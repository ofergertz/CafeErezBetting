using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CafeErezBetting.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Fetches Winner match data from the telesport.co.il web API (winnerzonepage.ashx).
/// The API requires a short-lived session token (c=...) that is extracted from the HTML
/// of the main Winner Zone page using a reverse-engineered obfuscation scheme:
///
///   HTML contains: script with c='PREFIX', hidden input id="dikiw" value="SUFFIX",
///                  and a span id="xmovp" with a single mid-char.
///   Token = unescape(PREFIX.slice(0,7) + xmovp + PREFIX+SUFFIX.slice(7))
///
/// Token is cached in memory for 30 minutes; refreshed automatically on expiry or failure.
/// </summary>
public sealed class TelesportWinnerApiClient(
    IHttpClientFactory httpFactory,
    ILogger<TelesportWinnerApiClient> logger) : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private static readonly Regex InvisibleChars =
        new(@"[​-‏‪-‮﻿­ ]+", RegexOptions.Compiled);

    private const string WinnerZonePage  = "https://www.telesport.co.il/%D7%90%D7%96%D7%95%D7%A8%20%D7%95%D7%95%D7%99%D7%A0%D7%A8";
    private const string WinnerZoneApi   = "https://www.telesport.co.il/ajaxactions/winnerzonepage.ashx";
    private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(30);

    private volatile string? _token;
    private DateTime _tokenAt;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    // ── Public ────────────────────────────────────────────────────────────────

    public async Task<List<WinnerMatchDto>> FetchMatchesAsync(CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        if (token is null)
        {
            logger.LogWarning("TelesportWinner: no session token — skipping");
            return [];
        }

        var date = DateTime.Today.ToString("yyyy-MM-ddTHH:mm:ss");
        var url  = $"{WinnerZoneApi}?c={Uri.EscapeDataString(token)}&DateNow={Uri.EscapeDataString(date)}&winnerPage=updateWinnerTablesByDate&program=1&allGames=true";

        var http = httpFactory.CreateClient("telesport-www");
        try
        {
            logger.LogInformation("TelesportWinner: GET {Url}", url);
            var json    = await http.GetStringAsync(url, ct);
            var records = JsonSerializer.Deserialize<List<TelesportWinnerRecord>>(json, JsonOpts);

            if (records is null || records.Count == 0)
            {
                logger.LogWarning("TelesportWinner: empty response — invalidating token");
                InvalidateToken();
                return [];
            }

            var result = MapToDto(records);
            logger.LogInformation("TelesportWinner: {Count} records → {Mapped} mapped", records.Count, result.Count);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TelesportWinner: winnerzonepage.ashx failed — invalidating token");
            InvalidateToken();
            return [];
        }
    }

    // ── Token management ──────────────────────────────────────────────────────

    private async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTime.UtcNow - _tokenAt < TokenTtl)
            return _token;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTime.UtcNow - _tokenAt < TokenTtl)
                return _token;

            var token = await ExtractTokenAsync(ct);
            if (token is not null)
            {
                _token  = token;
                _tokenAt = DateTime.UtcNow;
            }
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private void InvalidateToken() => _token = null;

    private async Task<string?> ExtractTokenAsync(CancellationToken ct)
    {
        var http = httpFactory.CreateClient("telesport-www");
        string html;
        try
        {
            html = await http.GetStringAsync(WinnerZonePage, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TelesportWinner: failed to fetch main page");
            return null;
        }

        // Step 1: script prefix  →  c='8%37%35'
        var prefixM = Regex.Match(html, @"c='([^']+)';c\+=eval");
        if (!prefixM.Success)
        {
            logger.LogWarning("TelesportWinner: token prefix not found in HTML");
            return null;
        }
        var prefix = prefixM.Groups[1].Value;

        // Step 2: hidden input id="dikiw"  →  value="7%64%61%30"
        var dikiwM = Regex.Match(html,
            @"<input[^>]+id=""dikiw""[^>]+value=""([^""]*?)""",
            RegexOptions.IgnoreCase);
        if (!dikiwM.Success)
            dikiwM = Regex.Match(html,
                @"<input[^>]+value=""([^""]*?)""[^>]+id=""dikiw""",
                RegexOptions.IgnoreCase);
        if (!dikiwM.Success)
        {
            logger.LogWarning("TelesportWinner: dikiw input not found in HTML");
            return null;
        }
        var dikiwValue = dikiwM.Groups[1].Value;

        // Step 3: span id="xmovp"  →  mid character (e.g. "6")
        var xmovpM = Regex.Match(html, @"<span[^>]+id=""xmovp"">([^<]+)</span>", RegexOptions.IgnoreCase);
        if (!xmovpM.Success)
        {
            logger.LogWarning("TelesportWinner: xmovp span not found in HTML");
            return null;
        }
        var mid = xmovpM.Groups[1].Value.Trim();

        // Step 4: reconstruct — mirrors the JS obfuscation:
        //   c = prefix + dikiw_value  (e.g. "8%37%357%64%61%30", length 17)
        //   encoded = c[0..7] + mid + c[7..]  (e.g. "8%37%35" + "6" + "7%64%61%30")
        //   token = unescape(encoded)           (e.g. "87567da0")
        var cCombined = prefix + dikiwValue;
        if (cCombined.Length < 7)
        {
            logger.LogWarning("TelesportWinner: c_combined too short ({Len})", cCombined.Length);
            return null;
        }
        var encoded = cCombined[..7] + mid + cCombined[7..];
        var token   = Uri.UnescapeDataString(encoded);

        logger.LogInformation("TelesportWinner: extracted token {Token}", token);
        return token;
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private List<WinnerMatchDto> MapToDto(List<TelesportWinnerRecord> records)
    {
        var result = new List<WinnerMatchDto>(records.Count);

        foreach (var r in records)
        {
            if (r.Rate1 is null || r.Rate2 is null) continue;

            var home = CleanName(r.Name1);
            var away = CleanName(r.Name2);
            if (home.Length < 2 || away.Length < 2) continue;

            var statusDisplay = WebUtility.HtmlDecode(r.StatusNameDisplay ?? "");
            var (isLive, minute) = ParseLiveStatus(statusDisplay);
            var isFinished = !isLive && IsFinishedStatus(statusDisplay, r.StatusId);

            var score = (isLive || isFinished) && r.Result1.HasValue && r.Result2.HasValue
                ? $"{r.Result1}-{r.Result2}"
                : null;

            var status = isLive ? "live" : isFinished ? "finished" : "upcoming";

            result.Add(new WinnerMatchDto(
                Guid.NewGuid(),
                $"wz-{r.LineNum}-{r.WinnerId}",
                home,
                away,
                r.LeagueNameDisplay ?? "ווינר",
                r.BetCloseDate ?? DateTime.UtcNow,
                new OddsDto(r.Rate1.Value, r.RateX ?? 0m, r.Rate2.Value),
                status,
                isLive,
                DateTime.UtcNow,
                r.TypeName is "רגיל" or null ? null : r.TypeName,
                FormNumber: r.LineNum.ToString()
            ));
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string CleanName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var decoded = WebUtility.HtmlDecode(raw);
        // Strip trailing handicap like " (+6.5)" or " (-1.5)"
        decoded = Regex.Replace(decoded, @"\s*\([^)]*\)\s*$", "").Trim();
        decoded = InvisibleChars.Replace(decoded, " ").Trim();
        decoded = Regex.Replace(decoded, @"  +", " ").Trim();
        return decoded.Length >= 2 ? decoded : raw.Trim();
    }

    /// <summary>"Q1 2'" → (true, "Q1 2")  "18:30" → (false, null)</summary>
    private static (bool IsLive, string? Minute) ParseLiveStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return (false, null);
        var s = status.Trim();

        if (s.EndsWith("'"))
            return (true, s[..^1].Trim());

        if (s.StartsWith("מחצית") || s.StartsWith("הפסקה") || s.StartsWith("HT") ||
            s.StartsWith("Q") || s.StartsWith("OT") || s.StartsWith("P"))
            return (true, s);

        if (s.Contains("חי") || s.Contains("Live") || s.Contains("בשידור"))
            return (true, null);

        return (false, null);
    }

    private static bool IsFinishedStatus(string status, int? statusId)
    {
        if (status is "סיום" or "הסתיים" or "FT" or "Final") return true;
        return statusId is 4 or 5; // adjust if server uses specific IDs for finished
    }

    public void Dispose() => _tokenLock.Dispose();
}

// ── JSON record ───────────────────────────────────────────────────────────────

internal sealed record TelesportWinnerRecord(
    [property: JsonPropertyName("lineNum")]             int       LineNum,
    [property: JsonPropertyName("winner_id")]           int       WinnerId,
    [property: JsonPropertyName("status_id")]           int?      StatusId,
    [property: JsonPropertyName("status_name_display")] string?   StatusNameDisplay,
    [property: JsonPropertyName("type_name")]           string?   TypeName,
    [property: JsonPropertyName("rate_1")]              decimal?  Rate1,
    [property: JsonPropertyName("rate_x")]              decimal?  RateX,
    [property: JsonPropertyName("rate_2")]              decimal?  Rate2,
    [property: JsonPropertyName("result_1")]            int?      Result1,
    [property: JsonPropertyName("result_2")]            int?      Result2,
    [property: JsonPropertyName("league_name_display")] string?   LeagueNameDisplay,
    [property: JsonPropertyName("name_1")]              string?   Name1,
    [property: JsonPropertyName("name_2")]              string?   Name2,
    [property: JsonPropertyName("betCloseDate")]        DateTime? BetCloseDate
);

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CafeErezBetting.Core.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Fetches Winner match data directly from the Telesport JSON API endpoint.
/// This is dramatically faster than Playwright HTML scraping and provides
/// clean structured data including league names, live scores, and bet types.
///
/// Endpoint: /ajaxactions/winnerzonepage.ashx?winnerPage=updateWinnerTablesByDate
/// Parameters: c (API key), DateNow (today midnight), program (1=Winner), allGames
/// </summary>
public class TelesportApiClient(
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<TelesportApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Invisible Unicode chars that appear in RTL team names after HTML decoding
    private static readonly Regex InvisibleChars =
        new(@"[​-‏‪-‮﻿­ ]", RegexOptions.Compiled);

    // Handicap suffix like " (+1)", " (-1.5)", " (+0.5)" appended to team name
    private static readonly Regex HandicapSuffix =
        new(@"\s*\(([+\-]\d+(?:\.\d+)?)\)\s*$", RegexOptions.Compiled);

    public async Task<List<WinnerMatchDto>> FetchWinnerMatchesAsync(CancellationToken ct = default)
    {
        var apiKey  = config["Scrapers:Winner:TelesportApiKey"] ?? "a33db2c2";
        var date    = DateTime.Today.ToString("yyyy-MM-ddT00:00:00");
        var url     = $"https://www.telesport.co.il/ajaxactions/winnerzonepage.ashx" +
                      $"?c={apiKey}&DateNow={Uri.EscapeDataString(date)}" +
                      $"&winnerPage=updateWinnerTablesByDate&program=1&allGames=true";

        logger.LogInformation("TelesportAPI: fetching {Url}", url);

        var http = httpFactory.CreateClient("telesport");
        try
        {
            var json = await http.GetStringAsync(url, ct);
            var records = JsonSerializer.Deserialize<List<TelesportWinnerRecord>>(json, JsonOptions);

            if (records is null || records.Count == 0)
            {
                logger.LogWarning("TelesportAPI: empty or null response");
                return [];
            }

            logger.LogInformation("TelesportAPI: received {Count} records", records.Count);
            var mapped = MapToDto(records);
            logger.LogInformation("TelesportAPI: mapped to {Count} matches", mapped.Count);
            return mapped;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TelesportAPI: request failed");
            return [];
        }
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private List<WinnerMatchDto> MapToDto(List<TelesportWinnerRecord> records)
    {
        // Count sub-markets per game (for SubMarket display)
        var subMarketCount = records
            .GroupBy(r => r.GameId)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<WinnerMatchDto>(records.Count);

        foreach (var r in records)
        {
            if (!r.ActiveToShow)             continue;
            if (r.Rate1 is null)             continue;
            if (r.Rate2 is null)             continue;

            var (homeTeam, _)         = ParseTeamName(r.Name1);
            var (awayTeam, handicap)  = ParseTeamName(r.Name2);

            if (homeTeam.Length < 2 || awayTeam.Length < 2) continue;

            var (isLive, minute) = ParseLiveStatus(r.StatusNameDisplay);
            var isFinished = !isLive && r.Result1.HasValue && r.Result2.HasValue;
            var score = (isLive || isFinished) && r.Result1.HasValue && r.Result2.HasValue
                ? $"{r.Result1}-{r.Result2}" : null;

            var betType = r.TypeName is "רגיל" or null ? null : r.TypeName;

            var betCode = (r.IsSingle, r.IsDouble) switch
            {
                (true,  true)  => "S,D",
                (true,  false) => "S",
                (false, true)  => "D",
                _              => (string?)null,
            };

            var sub = subMarketCount.TryGetValue(r.GameId, out var cnt) ? cnt : (int?)null;
            var league = r.LeagueNameDisplay ?? "ווינר";
            var status = isLive ? "live" : isFinished ? "finished" : "upcoming";

            result.Add(new WinnerMatchDto(
                Guid.NewGuid(),
                $"ts-{r.WinnerId}",
                homeTeam,
                awayTeam,
                league,
                r.BetCloseDate,
                new OddsDto(r.Rate1.Value, r.RateX, r.Rate2.Value),
                status,
                isLive,
                DateTime.UtcNow,
                betType,
                handicap,
                sub,
                r.WinnerId.ToString(),
                score,
                minute,
                betCode,
                r.LeagueImageUrl
            ));
        }

        return result;
    }

    // ── Team name helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Decodes HTML entities (&amp;#160; nbsp, &amp;#8206; LRM), strips invisible Unicode
    /// directional marks, and extracts trailing handicap notation like "(+1.5)".
    /// </summary>
    private static (string Name, string? Handicap) ParseTeamName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ("", null);

        // Decode: &amp;#160; → &#160; → non-breaking space, etc.
        var decoded = WebUtility.HtmlDecode(raw);

        // Strip invisible / directional Unicode chars
        decoded = InvisibleChars.Replace(decoded, " ").Trim();

        // Collapse multiple spaces
        decoded = Regex.Replace(decoded, @"  +", " ").Trim();

        // Extract handicap suffix like "(+1)" or "(-1.5)"
        string? handicap = null;
        var hm = HandicapSuffix.Match(decoded);
        if (hm.Success)
        {
            handicap = hm.Groups[1].Value;
            decoded  = decoded[..hm.Index].Trim();
        }

        return (decoded.Length >= 2 ? decoded : raw.Trim(), handicap);
    }

    // ── Live / minute helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Parses live status and match minute from status_name_display.
    /// "52'" → (true, "52")   "Q1&#160;6'" → (true, "Q1 6")   "מחצית 1" → (true, "מחצית 1")
    /// </summary>
    private static (bool IsLive, string? Minute) ParseLiveStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (false, null);

        var s = WebUtility.HtmlDecode(raw).Trim();
        // Collapse nbsp/invisible chars to regular space
        s = Regex.Replace(s, @"[ ​-‏]", " ").Trim();

        // Ends with apostrophe → "52'" or "Q1 6'"
        if (s.EndsWith("'"))
            return (true, s[..^1].Trim());

        // Known live phrases (half-time, quarter, break, overtime)
        if (s.StartsWith("מחצית") || s.StartsWith("הפסקה") ||
            s.StartsWith("Q") || s.StartsWith("OT") || s.StartsWith("HT"))
            return (true, s);

        return (false, null);
    }

}

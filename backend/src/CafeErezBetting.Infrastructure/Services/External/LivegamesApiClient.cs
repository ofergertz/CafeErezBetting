using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CafeErezBetting.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Fetches Winner match data from the Livegames or Telesport mobile JSON APIs.
/// Both endpoints share the same JSON structure (LivegamesWinnerRecord).
///
/// Livegames:        https://m.livegames.co.il/api/winner?date=yyyy-MM-dd
/// Telesport mobile: https://m.telesport.co.il/api/winner?date=yyyy-MM-dd
/// </summary>
public class LivegamesApiClient(
    IHttpClientFactory httpFactory,
    ILogger<LivegamesApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Invisible Unicode directional chars
    private static readonly Regex InvisibleChars =
        new(@"[​-‏‪-‮﻿­ ]", RegexOptions.Compiled);

    private const string LivegamesBaseUrl       = "https://m.livegames.co.il/api/winner";
    private const string TelesportMobileBaseUrl = "https://m.telesport.co.il/api/winner";

    public Task<List<WinnerMatchDto>> FetchWinnerMatchesAsync(CancellationToken ct = default)
        => FetchFromAsync(LivegamesBaseUrl, "livegames", ct);

    public Task<List<WinnerMatchDto>> FetchTelesportMobileAsync(CancellationToken ct = default)
        => FetchFromAsync(TelesportMobileBaseUrl, "telesport", ct);

    // ── Core fetch ────────────────────────────────────────────────────────────

    private async Task<List<WinnerMatchDto>> FetchFromAsync(string baseUrl, string clientName, CancellationToken ct)
    {
        var date = DateTime.Today.ToString("yyyy-MM-dd");
        var url  = $"{baseUrl}?date={date}";

        logger.LogInformation("WinnerAPI [{Client}]: fetching {Url}", clientName, url);

        var http = httpFactory.CreateClient(clientName);
        try
        {
            var json    = await http.GetStringAsync(url, ct);
            var records = JsonSerializer.Deserialize<List<LivegamesWinnerRecord>>(json, JsonOptions);

            if (records is null || records.Count == 0)
            {
                logger.LogWarning("WinnerAPI [{Client}]: empty response", clientName);
                return [];
            }

            logger.LogInformation("WinnerAPI [{Client}]: received {Count} total records", clientName, records.Count);

            // activetoshow=false for finished/upcoming — keep any record that has odds
            var active = records.Where(r => r.Rate1.HasValue && r.Rate2.HasValue).ToList();
            logger.LogInformation("WinnerAPI [{Client}]: {Count} records with odds", clientName, active.Count);

            return MapToDto(active, clientName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WinnerAPI [{Client}]: request failed", clientName);
            return [];
        }
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private List<WinnerMatchDto> MapToDto(List<LivegamesWinnerRecord> records, string clientName)
    {
        var prefix = clientName == "telesport" ? "ts-m" : "lg";

        var subMarketCount = records
            .Where(r => r.Game is not null)
            .GroupBy(r => r.Game!.Id)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<WinnerMatchDto>(records.Count);

        foreach (var r in records)
        {
            var homeTeam = CleanName(r.P1Name);
            var awayTeam = CleanName(r.P2Name);
            if (homeTeam.Length < 2 || awayTeam.Length < 2) continue;

            var game = r.Game;

            var (isLive, minute) = ParseLiveStatus(r.StatusFormatted, r.StatusName);
            // Livegames uses "סיום"; Telesport mobile uses "הסתיים"
            var isFinished = !isLive && (
                r.StatusFormatted is "סיום" or "הסתיים" ||
                r.StatusName      is "סיום" or "הסתיים");
            var score = (isLive || isFinished)
                ? (r.P1Score.HasValue && r.P2Score.HasValue
                    ? $"{r.P1Score}-{r.P2Score}"
                    : r.Result1.HasValue && r.Result2.HasValue
                        ? $"{r.Result1}-{r.Result2}"
                        : null)
                : null;

            var status = isLive ? "live" : isFinished ? "finished" : "upcoming";
            var betType = r.TypeName is "רגיל" or null ? null : r.TypeName;
            var league = game?.LeagueName ?? "ווינר";
            var leagueImageUrl = game?.LeagueImageUrl;
            var scheduledAt = game?.StartDate ?? r.BetCloseDate;
            var sub = game is not null && subMarketCount.TryGetValue(game.Id, out var cnt) ? cnt : (int?)null;

            result.Add(new WinnerMatchDto(
                Guid.NewGuid(),
                $"{prefix}-{r.WinnerId}",
                homeTeam,
                awayTeam,
                league,
                scheduledAt,
                new OddsDto(r.Rate1!.Value, r.RateX, r.Rate2!.Value),
                status,
                isLive,
                DateTime.UtcNow,
                betType,
                Handicap:       null,
                SubMarket:      sub,
                FormNumber:     r.LineNum.ToString(),
                Score:          score,
                Minute:         minute,
                BetCode:        null,
                LeagueImageUrl: leagueImageUrl
            ));
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string CleanName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var decoded = WebUtility.HtmlDecode(raw);
        decoded = InvisibleChars.Replace(decoded, " ").Trim();
        decoded = Regex.Replace(decoded, @"  +", " ").Trim();
        return decoded.Length >= 2 ? decoded : raw.Trim();
    }

    /// <summary>
    /// "55'" → (true, "55")   "מחצית" → (true, "מחצית")   "22:00" → (false, null)
    /// </summary>
    private static (bool IsLive, string? Minute) ParseLiveStatus(string? formatted, string? statusName)
    {
        if (!string.IsNullOrWhiteSpace(formatted))
        {
            var s = formatted.Trim();
            if (s.EndsWith("'"))
                return (true, s[..^1].Trim());
            if (s.StartsWith("מחצית") || s.StartsWith("הפסקה") ||
                s.StartsWith("Q") || s.StartsWith("OT") || s.StartsWith("HT"))
                return (true, s);
        }

        if (!string.IsNullOrWhiteSpace(statusName))
        {
            var n = statusName.Trim();
            if (n.Contains("חי") || n.Contains("בשידור") || n.Contains("Live"))
                return (true, null);
        }

        return (false, null);
    }
}

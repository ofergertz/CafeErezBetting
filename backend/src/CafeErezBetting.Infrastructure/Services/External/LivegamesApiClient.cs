using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CafeErezBetting.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Fetches Winner match data from the Livegames JSON API.
/// Endpoint: https://m.livegames.co.il/api/winner?date=yyyy-MM-dd
/// Returns one record per betting market (same structure as Telesport).
/// Used as a secondary source when Telesport is unavailable.
/// </summary>
public class LivegamesApiClient(
    IHttpClientFactory httpFactory,
    ILogger<LivegamesApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Invisible Unicode directional chars (same cleanup as Telesport)
    private static readonly Regex InvisibleChars =
        new(@"[​-‏‪-‮﻿­ ]", RegexOptions.Compiled);

    public async Task<List<WinnerMatchDto>> FetchWinnerMatchesAsync(CancellationToken ct = default)
    {
        var date = DateTime.Today.ToString("yyyy-MM-dd");
        var url  = $"https://m.livegames.co.il/api/winner?date={date}";

        logger.LogInformation("LivegamesAPI: fetching {Url}", url);

        var http = httpFactory.CreateClient("livegames");
        try
        {
            var json    = await http.GetStringAsync(url, ct);
            var records = JsonSerializer.Deserialize<List<LivegamesWinnerRecord>>(json, JsonOptions);

            if (records is null || records.Count == 0)
            {
                logger.LogWarning("LivegamesAPI: empty response");
                return [];
            }

            logger.LogInformation("LivegamesAPI: received {Count} total records", records.Count);

            var active = records.Where(r => r.ActiveToShow && r.Rate1.HasValue && r.Rate2.HasValue).ToList();
            logger.LogInformation("LivegamesAPI: {Count} active records with odds", active.Count);

            return MapToDto(active);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LivegamesAPI: request failed");
            return [];
        }
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private List<WinnerMatchDto> MapToDto(List<LivegamesWinnerRecord> records)
    {
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
            var hasScore = r.P1Score.HasValue && r.P2Score.HasValue;
            var isFinished = !isLive && hasScore;
            var score = (isLive || isFinished) && hasScore
                ? $"{r.P1Score}-{r.P2Score}"
                : null;

            var status = isLive ? "live" : isFinished ? "finished" : "upcoming";
            var betType = r.TypeName is "רגיל" or null ? null : r.TypeName;
            var league = game?.LeagueName ?? "ווינר";
            var leagueImageUrl = game?.LeagueImageUrl;
            var scheduledAt = game?.StartDate ?? r.BetCloseDate;
            var sub = game is not null && subMarketCount.TryGetValue(game.Id, out var cnt) ? cnt : (int?)null;

            result.Add(new WinnerMatchDto(
                Guid.NewGuid(),
                $"lg-{r.WinnerId}",
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

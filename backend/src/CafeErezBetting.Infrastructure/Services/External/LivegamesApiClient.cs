using System.Text.Json;
using CafeErezBetting.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Fetches Winner match data from the Livegames mobile JSON API.
/// Returns one clean record per game (vs Telesport which returns one record per betting market).
/// Used as a secondary source to supplement or replace Telesport data when needed.
///
/// Endpoint: https://m.livegames.co.il/api/results?date=yyyy-MM-dd
/// Flag image: media_url field (country flag, not league logo)
/// </summary>
public class LivegamesApiClient(
    IHttpClientFactory httpFactory,
    ILogger<LivegamesApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // status_id values from livegames
    private const int StatusFinished  = 100;
    private const int StatusLive      = 2;
    private const int StatusSuspended = 3;

    public async Task<List<WinnerMatchDto>> FetchWinnerMatchesAsync(CancellationToken ct = default)
    {
        var date = DateTime.Today.ToString("yyyy-MM-dd");
        var url  = $"https://m.livegames.co.il/api/results?date={date}";

        logger.LogInformation("LivegamesAPI: fetching {Url}", url);

        var http = httpFactory.CreateClient("livegames");
        try
        {
            var json    = await http.GetStringAsync(url, ct);
            var records = JsonSerializer.Deserialize<List<LivegamesMatchRecord>>(json, JsonOptions);

            if (records is null || records.Count == 0)
            {
                logger.LogWarning("LivegamesAPI: empty response");
                return [];
            }

            logger.LogInformation("LivegamesAPI: received {Count} total records", records.Count);

            // Filter to Winner-only matches with valid odds
            var winnerRecords = records
                .Where(r => r.Winner && r.WinRate1.HasValue && r.WinRate2.HasValue)
                .ToList();

            logger.LogInformation("LivegamesAPI: {Count} Winner records with odds", winnerRecords.Count);
            return MapToDto(winnerRecords);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LivegamesAPI: request failed");
            return [];
        }
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static List<WinnerMatchDto> MapToDto(List<LivegamesMatchRecord> records)
    {
        var result = new List<WinnerMatchDto>(records.Count);
        var index  = 0;

        foreach (var r in records)
        {
            var homeTeam = r.P1Name?.Trim() ?? "";
            var awayTeam = r.P2Name?.Trim() ?? "";
            if (homeTeam.Length < 2 || awayTeam.Length < 2) continue;

            var isLive  = DetermineIsLive(r);
            var status  = DetermineStatus(r);
            var minute  = isLive ? ParseMinute(r.StatusNameDisplay) : null;
            var score   = (isLive || status == "finished") && r.P1Score.HasValue && r.P2Score.HasValue
                ? $"{r.P1Score}-{r.P2Score}"
                : null;

            var league = r.LeagueNameDisplay ?? "ווינר";

            result.Add(new WinnerMatchDto(
                Guid.NewGuid(),
                $"lg-{r.Id}",
                homeTeam,
                awayTeam,
                league,
                r.StartDateIso,
                new OddsDto(r.WinRate1!.Value, r.WinRateX, r.WinRate2!.Value),
                status,
                isLive,
                DateTime.UtcNow,
                BetType:       null,
                Handicap:      null,
                SubMarket:     null,
                FormNumber:    null,
                Score:         score,
                Minute:        minute,
                BetCode:       null,
                LeagueImageUrl: r.MediaUrl ?? r.LeagueImageUrl
            ));

            index++;
        }

        return result;
    }

    // ── Status helpers ────────────────────────────────────────────────────────

    private static bool DetermineIsLive(LivegamesMatchRecord r)
    {
        if (r.Active) return true;
        if (r.StatusId == StatusLive) return true;

        // Check status display for minute indicator
        var display = r.StatusNameDisplay;
        if (string.IsNullOrWhiteSpace(display)) return false;
        return display.EndsWith("'") || display.Contains("מחצית") || display.Contains("הפסקה");
    }

    private static string DetermineStatus(LivegamesMatchRecord r)
    {
        if (r.StatusId == StatusFinished)  return "finished";
        if (r.StatusId == StatusSuspended) return "suspended";
        if (DetermineIsLive(r))            return "live";
        return "upcoming";
    }

    /// <summary>Extracts minute string from status_name_display like "52'" → "52".</summary>
    private static string? ParseMinute(string? display)
    {
        if (string.IsNullOrWhiteSpace(display)) return null;
        if (display.EndsWith("'"))  return display[..^1].Trim();
        if (display.Contains("מחצית") || display.Contains("הפסקה")) return display;
        return null;
    }
}

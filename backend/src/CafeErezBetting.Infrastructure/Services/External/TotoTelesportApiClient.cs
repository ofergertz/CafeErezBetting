using System.Text.Json;
using CafeErezBetting.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Fetches Toto 16 data from the Telesport or Livegames mobile JSON API.
/// Both endpoints share the same JSON structure, so this client handles both.
///
/// Telesport:  https://m.telesport.co.il/api/toto?date=yyyy-MM-dd
/// Livegames:  https://m.livegames.co.il/api/toto?date=yyyy-MM-dd
/// </summary>
public class TotoTelesportApiClient(
    IHttpClientFactory httpFactory,
    ILogger<TotoTelesportApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<TotoRoundDto?> FetchAsync(string baseUrl, string httpClientName, CancellationToken ct = default)
    {
        var date = DateTime.Today.ToString("yyyy-MM-dd");
        var url  = $"{baseUrl}?date={date}";

        logger.LogInformation("TotoAPI [{Client}]: fetching {Url}", httpClientName, url);

        var http = httpFactory.CreateClient(httpClientName);
        try
        {
            var json    = await http.GetStringAsync(url, ct);
            var records = JsonSerializer.Deserialize<List<TotoTelesportRecord>>(json, JsonOptions);

            if (records is null || records.Count == 0)
            {
                logger.LogWarning("TotoAPI [{Client}]: empty response", httpClientName);
                return null;
            }

            logger.LogInformation("TotoAPI [{Client}]: {Count} matches", httpClientName, records.Count);
            return MapToDto(records);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TotoAPI [{Client}]: request failed", httpClientName);
            return null;
        }
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static TotoRoundDto MapToDto(List<TotoTelesportRecord> records)
    {
        var first       = records[0];
        var roundNumber = first.LineNum;
        var roundId     = roundNumber.ToString();

        var matches = records
            .Where(r => !string.IsNullOrWhiteSpace(r.Name1) && !string.IsNullOrWhiteSpace(r.Name2))
            .Select(r => new TotoMatchDto(
                Id:          r.TotoId.ToString(),
                HomeTeam:    r.Name1!.Trim(),
                AwayTeam:    r.Name2!.Trim(),
                League:      r.LeagueName ?? "טוטו",
                ScheduledAt: ParseScheduledAt(r.StatusDisplay),
                Result:      r.Result
            ))
            .ToList();

        return new TotoRoundDto(roundId, roundNumber, matches);
    }

    /// <summary>Parses "14/06 23:00" (dd/MM HH:mm, Israeli local time) into UTC DateTime.</summary>
    private static DateTime? ParseScheduledAt(string? display)
    {
        if (string.IsNullOrWhiteSpace(display)) return null;

        var parts = display.Split(' ');
        if (parts.Length < 2) return null;

        var dateParts = parts[0].Split('/');
        var timeParts = parts[1].Split(':');
        if (dateParts.Length < 2 || timeParts.Length < 2) return null;

        if (!int.TryParse(dateParts[0], out var day)   ||
            !int.TryParse(dateParts[1], out var month)  ||
            !int.TryParse(timeParts[0], out var hour)   ||
            !int.TryParse(timeParts[1], out var minute))
            return null;

        var year = DateTime.Today.Year;
        // If the parsed date is in the past, assume next year
        if (month < DateTime.Today.Month || (month == DateTime.Today.Month && day < DateTime.Today.Day))
            year++;

        try
        {
            // Israeli time (UTC+3 summer / UTC+2 winter) — subtract 3h as a reasonable approximation
            var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
            return DateTime.SpecifyKind(local, DateTimeKind.Utc);
        }
        catch
        {
            return null;
        }
    }
}

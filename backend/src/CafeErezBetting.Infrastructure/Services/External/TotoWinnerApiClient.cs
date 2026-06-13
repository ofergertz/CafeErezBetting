using System.Text.Json;
using CafeErezBetting.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Fetches Toto 16 data from the Winner.co.il public API.
/// Endpoint: https://www.winner.co.il/api/v2/publicapi/GetTotoDraws
/// Used as a tertiary source when Telesport and Livegames are unavailable.
/// </summary>
public class TotoWinnerApiClient(
    IHttpClientFactory httpFactory,
    ILogger<TotoWinnerApiClient> logger)
{
    private const string Url       = "https://www.winner.co.il/api/v2/publicapi/GetTotoDraws";
    private const int    Toto16GameType = 87;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<TotoRoundDto?> FetchAsync(CancellationToken ct = default)
    {
        logger.LogInformation("WinnerTotoAPI: fetching {Url}", Url);

        var http = httpFactory.CreateClient("winner");
        try
        {
            var json     = await http.GetStringAsync(Url, ct);
            var response = JsonSerializer.Deserialize<TotoWinnerResponse>(json, JsonOptions);

            var toto16 = response?.Games.FirstOrDefault(g => g.GameType == Toto16GameType);
            if (toto16 is null || toto16.Rows is null || toto16.Rows.Count == 0)
            {
                logger.LogWarning("WinnerTotoAPI: no Toto-16 data (gameType={Type})", Toto16GameType);
                return null;
            }

            logger.LogInformation("WinnerTotoAPI: {Count} rows, draw #{DrawNumber}",
                toto16.Rows.Count, toto16.DrawNumber);

            return MapToDto(toto16);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WinnerTotoAPI: request failed");
            return null;
        }
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static TotoRoundDto MapToDto(TotoWinnerGame game)
    {
        var drawNumber = game.DrawNumber ?? 0;
        var roundId    = drawNumber.ToString();

        var matches = (game.Rows ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r.TeamA) && !string.IsNullOrWhiteSpace(r.TeamB))
            .Select(r => new TotoMatchDto(
                Id:          r.RowNumber.ToString(),
                HomeTeam:    r.TeamA!.Trim(),
                AwayTeam:    r.TeamB!.Trim(),
                League:      r.League ?? "טוטו",
                ScheduledAt: DateTime.SpecifyKind(r.EventStartTime, DateTimeKind.Utc),
                Result:      null
            ))
            .ToList();

        return new TotoRoundDto(roundId, drawNumber, matches);
    }
}

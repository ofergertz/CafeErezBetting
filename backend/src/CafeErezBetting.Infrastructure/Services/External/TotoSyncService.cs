using System.Text.Json;
using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Interfaces.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Syncs Toto round data from external JSON APIs (backend-only, never client).
/// Primary:    Telesport  (m.telesport.co.il/api/toto)
/// Secondary:  Livegames  (m.livegames.co.il/api/toto)
/// Tertiary:   Winner.co.il (www.winner.co.il/api/v2/publicapi/GetTotoDraws)
/// Falls back to Redis cache or mock data when all APIs fail.
/// </summary>
public class TotoSyncService(
    IDistributedCache cache,
    IConfiguration config,
    TotoTelesportApiClient telesportApi,
    TotoWinnerApiClient winnerApi,
    ILogger<TotoSyncService> logger
) : ITotoSyncService
{
    private const string CacheKey    = "toto:current_round";
    private const string LastSyncKey = "toto:last_sync";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private const string TelesportTotoUrl = "https://m.telesport.co.il/api/toto";
    private const string LivegamesTotoUrl = "https://m.livegames.co.il/api/toto";

    public async Task<TotoRoundDto?> GetCurrentRoundAsync(CancellationToken ct = default)
    {
        try
        {
            var cached = await cache.GetStringAsync(CacheKey, ct);
            if (cached is not null)
            {
                try { return JsonSerializer.Deserialize<TotoRoundDto>(cached); }
                catch { /* corrupted cache — fall through */ }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Toto: Redis unavailable — falling back to mock data");
        }

        return GetMockData();
    }

    public async Task<SyncStatusDto> SyncNowAsync(CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Toto sync started");

            var (round, isMock) = await ScrapeExternalAsync(ct);

            if (round is not null)
                await CacheRoundAsync(round, ct);

            var dto = new SyncStatusDto(true, DateTime.UtcNow, null, isMock);
            await cache.SetStringAsync(
                LastSyncKey,
                JsonSerializer.Serialize(dto),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1) },
                ct
            );

            logger.LogInformation("Toto sync completed: {Count} matches (mock={IsMock})",
                round?.Matches.Count ?? 0, isMock);
            return dto;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Toto sync failed");
            return new SyncStatusDto(false, DateTime.UtcNow, ex.Message, true);
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task<(TotoRoundDto? Round, bool IsMock)> ScrapeExternalAsync(CancellationToken ct)
    {
        var useRealData = config.GetValue<bool>("Scrapers:Toto:UseRealData", defaultValue: false);
        if (!useRealData)
        {
            logger.LogDebug("Toto: UseRealData=false — returning mock data");
            return (GetMockData(), true);
        }

        // 1. Primary: Telesport
        try
        {
            var round = await telesportApi.FetchAsync(TelesportTotoUrl, "telesport", ct);
            if (round is not null && round.Matches.Count > 0)
            {
                logger.LogInformation("Toto Telesport sync succeeded: {Count} matches", round.Matches.Count);
                return (round, false);
            }
            logger.LogWarning("Toto Telesport returned 0 matches — trying Livegames");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Toto Telesport failed — trying Livegames");
        }

        // 2. Secondary: Livegames
        try
        {
            var round = await telesportApi.FetchAsync(LivegamesTotoUrl, "livegames", ct);
            if (round is not null && round.Matches.Count > 0)
            {
                logger.LogInformation("Toto Livegames sync succeeded: {Count} matches", round.Matches.Count);
                return (round, false);
            }
            logger.LogWarning("Toto Livegames returned 0 matches — trying Winner.co.il");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Toto Livegames failed — trying Winner.co.il");
        }

        // 3. Tertiary: Winner.co.il
        try
        {
            var round = await winnerApi.FetchAsync(ct);
            if (round is not null && round.Matches.Count > 0)
            {
                logger.LogInformation("Toto Winner.co.il sync succeeded: {Count} matches", round.Matches.Count);
                return (round, false);
            }
            logger.LogWarning("Toto Winner.co.il returned 0 matches — falling back to mock");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Toto Winner.co.il failed — falling back to mock");
        }

        return (GetMockData(), true);
    }

    private async Task CacheRoundAsync(TotoRoundDto round, CancellationToken ct)
    {
        await cache.SetStringAsync(
            CacheKey,
            JsonSerializer.Serialize(round),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
            ct
        );
    }

    private static TotoRoundDto GetMockData() => new(
        "toto-mock-001", 1001,
        new List<TotoMatchDto>
        {
            new("m1",  "מכבי תל אביב",   "הפועל ב\"ש",   "ליגת העל"),
            new("m2",  "בית\"ר ירושלים", "מכבי חיפה",    "ליגת העל"),
            new("m3",  "Real Madrid",     "Barcelona",     "La Liga"),
            new("m4",  "Man City",        "Arsenal",       "Premier League"),
            new("m5",  "Bayern",          "Dortmund",      "Bundesliga"),
            new("m6",  "PSG",             "Lyon",          "Ligue 1"),
            new("m7",  "Juventus",        "Inter",         "Serie A"),
            new("m8",  "Ajax",            "PSV",           "Eredivisie"),
            new("m9",  "הפועל ת\"א",      "מ.פ. תל אביב", "ליגת העל"),
            new("m10", "Atletico",        "Sevilla",       "La Liga"),
            new("m11", "Chelsea",         "Liverpool",     "Premier League"),
            new("m12", "Roma",            "Lazio",         "Serie A"),
            new("m13", "Porto",           "Benfica",       "Primeira Liga"),
        },
        IsMock: true);
}

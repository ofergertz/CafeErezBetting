using System.Text.Json;
using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Interfaces.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Syncs Toto round data from external sources (backend-only, never client).
/// Uses Redis for caching — no DB persistence needed (rounds change weekly).
/// Falls back to mock data on failure.
/// </summary>
public class TotoSyncService(
    IDistributedCache cache,
    IConfiguration config,
    IHostEnvironment env,
    PlaywrightTotoScraper playwright,
    ILogger<TotoSyncService> logger
) : ITotoSyncService
{
    private const string CacheKey    = "toto:current_round";
    private const string LastSyncKey = "toto:last_sync";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public async Task<TotoRoundDto?> GetCurrentRoundAsync(CancellationToken ct = default)
    {
        // 1. Try Redis cache first
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

        // 2. Fall back to mock
        return GetMockData();
    }

    public async Task<SyncStatusDto> SyncNowAsync(CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Toto sync started");

            var (round, isMock) = await ScrapeExternalAsync(ct);

            if (round is not null)
            {
                await CacheRoundAsync(round, ct);
            }

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

    /// <summary>
    /// Scrape from external source.
    /// Development: returns mock data unless Scrapers:Toto:UseRealData=true.
    /// Production: uses Playwright; falls back to mock on failure.
    /// </summary>
    private async Task<(TotoRoundDto? Round, bool IsMock)> ScrapeExternalAsync(CancellationToken ct)
    {
        var useRealData = config.GetValue<bool>("Scrapers:Toto:UseRealData", defaultValue: false);
        if (env.IsDevelopment() && !useRealData)
        {
            await Task.Delay(50, ct);
            logger.LogDebug("Toto development mode: returning mock data (set Scrapers:Toto:UseRealData=true to scrape real data)");
            return (GetMockData(), true);
        }

        try
        {
            var scraped = await playwright.ScrapeAsync(ct);
            if (scraped is not null && scraped.Matches.Count > 0)
            {
                logger.LogInformation("Playwright Toto scrape succeeded: {Count} matches", scraped.Matches.Count);
                return (scraped, false);
            }
            logger.LogWarning("Playwright Toto returned 0 matches — falling back to mock data");
            return (GetMockData(), true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Playwright Toto scrape failed — falling back to mock data");
            return (GetMockData(), true);
        }
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

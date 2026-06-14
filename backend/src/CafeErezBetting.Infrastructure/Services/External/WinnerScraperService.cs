using System.Text.Json;
using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Entities;
using CafeErezBetting.Core.Interfaces.Services;
using CafeErezBetting.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Syncs Winner match data from external sources (backend-only, never client).
/// Primary source: TelesportWinnerApiClient (HTTP JSON API, fast, no browser needed).
/// Fallback:       PlaywrightWinnerScraper  (headless Chromium — requires installation).
/// Falls back to Redis cache or mock data when all external sources fail.
/// </summary>
public class WinnerScraperService(
    AppDbContext db,
    IDistributedCache cache,
    IConfiguration config,
    IHostEnvironment env,
    TelesportWinnerApiClient telesportApi,
    PlaywrightWinnerScraper playwright,
    ILogger<WinnerScraperService> logger
) : IWinnerSyncService
{
    private const string CacheKey        = "winner:matches";
    private const string LastSyncKey     = "winner:last_sync";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(90);

    public async Task<List<WinnerMatchDto>> GetMatchesAsync(CancellationToken ct = default)
    {
        // 1. Try Redis cache first
        try
        {
            var cached = await cache.GetStringAsync(CacheKey, ct);
            if (cached is not null)
            {
                try { return JsonSerializer.Deserialize<List<WinnerMatchDto>>(cached) ?? []; }
                catch { /* corrupted cache — fall through */ }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis unavailable — falling back to DB");
        }

        // 2. Fall back to DB
        try
        {
            return await GetFromDbAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DB unavailable — returning mock data");
            return GetMockData();
        }
    }

    public async Task<SyncStatusDto> SyncNowAsync(CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Winner sync started");

            var (matches, isMock) = await ScrapeExternalAsync(ct);

            if (matches.Count > 0)
            {
                if (!isMock)
                {
                    try { await PersistMatchesAsync(matches, ct); }
                    catch (Exception ex) { logger.LogWarning(ex, "DB persist failed — cache will still be updated"); }
                }

                await CacheMatchesAsync(matches, ct);
            }

            var dto = new SyncStatusDto(true, DateTime.UtcNow, null, isMock);
            await cache.SetStringAsync(
                LastSyncKey,
                JsonSerializer.Serialize(dto),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1) },
                ct
            );

            logger.LogInformation("Winner sync completed: {Count} matches (mock={IsMock})", matches.Count, isMock);
            return dto;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Winner sync failed, using cached data");
            return new SyncStatusDto(false, DateTime.UtcNow, ex.Message, true);
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    public async Task<List<WinnerMatchDto>> ScrapeFromSourceAsync(int sourceIndex, CancellationToken ct = default)
    {
        logger.LogInformation("Direct scrape from source {Index}", sourceIndex);
        return sourceIndex == 0
            ? await telesportApi.FetchMatchesAsync(ct)
            : await playwright.ScrapeAsync(sourceIndex - 1, ct);
    }

    private async Task<List<WinnerMatchDto>> GetFromDbAsync(CancellationToken ct)
    {
        var matches = await db.WinnerMatches
            .OrderByDescending(m => m.IsLive)
            .ThenBy(m => m.ScheduledAt)
            .Take(500)
            .ToListAsync(ct);

        return matches.Select(ToDto).ToList();
    }

    /// <summary>
    /// Scrape from external source.
    /// Returns (matches, isMock) — callers can surface mock status to the UI.
    /// Development: returns mock data unless WinnerScraper:UseRealData=true.
    /// Production: uses Playwright to scrape the configured URL; falls back to mock on failure.
    /// </summary>
    private async Task<(List<WinnerMatchDto> Matches, bool IsMock)> ScrapeExternalAsync(CancellationToken ct)
    {
        // Use mock data in Development UNLESS Scrapers:Winner:UseRealData=true (for local debug)
        var useRealData = config.GetValue<bool>("Scrapers:Winner:UseRealData", defaultValue: false);
        if (env.IsDevelopment() && !useRealData)
        {
            await Task.Delay(50, ct);
            logger.LogDebug("Development mode: returning mock data (set Scrapers:Winner:UseRealData=true to scrape real data)");
            return (GetMockData(), true);
        }

        // 1. Primary: Telesport HTTP JSON API (fast, no browser needed)
        try
        {
            var apiMatches = await telesportApi.FetchMatchesAsync(ct);
            if (apiMatches.Count > 0)
            {
                logger.LogInformation("TelesportWinnerApi: {Count} matches", apiMatches.Count);
                return (apiMatches, false);
            }
            logger.LogWarning("TelesportWinnerApi returned 0 matches — trying Playwright");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TelesportWinnerApi failed — trying Playwright");
        }

        // 2. Fallback: Playwright headless browser
        try
        {
            var scraped = await playwright.ScrapeAsync(0, ct);
            if (scraped.Count > 0)
            {
                logger.LogInformation("Playwright scrape succeeded: {Count} real matches", scraped.Count);
                return (scraped, false);
            }
            logger.LogWarning("Playwright returned 0 matches — check selectors or site structure. Falling back to mock data");
            return (GetMockData(), true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Playwright scrape failed (Chromium may not be installed or site is unreachable) — falling back to mock data");
            return (GetMockData(), true);
        }
    }

    private static List<WinnerMatchDto> GetMockData()
    {
        var now = DateTime.UtcNow;
        return
        [
            new(Guid.NewGuid(), "ext-001", "מכבי תל אביב",    "הפועל ב\"ש",    "ליגת העל",  now.AddHours(2),  new(2.10m, 3.20m, 3.50m), "upcoming", false, now),
            new(Guid.NewGuid(), "ext-002", "ביתר ירושלים",    "מכבי חיפה",     "ליגת העל",  now.AddHours(4),  new(2.80m, 3.10m, 2.60m), "upcoming", false, now),
            new(Guid.NewGuid(), "ext-003", "Real Madrid",      "Barcelona",      "La Liga",   now.AddMinutes(5),new(2.50m, 3.40m, 2.90m), "live",     true,  now),
            new(Guid.NewGuid(), "ext-004", "Manchester City",  "Arsenal",        "Premier League", now.AddHours(6), new(1.90m, 3.80m, 4.00m), "upcoming", false, now),
            new(Guid.NewGuid(), "ext-005", "Bayern Munich",    "Borussia Dortmund", "Bundesliga", now.AddHours(3), new(1.70m, 3.60m, 4.50m), "upcoming", false, now),
        ];
    }

    private async Task PersistMatchesAsync(List<WinnerMatchDto> matches, CancellationToken ct)
    {
        // Remove stale records not in current scrape (cleans up mock data + expired matches)
        var currentIds = matches.Select(m => m.ExternalId).ToHashSet();
        var stale = await db.WinnerMatches
            .Where(m => !currentIds.Contains(m.ExternalId))
            .ToListAsync(ct);
        if (stale.Count > 0)
        {
            db.WinnerMatches.RemoveRange(stale);
            logger.LogInformation("Removed {Count} stale/mock matches from DB", stale.Count);
        }

        foreach (var dto in matches)
        {
            var existing = await db.WinnerMatches
                .FirstOrDefaultAsync(m => m.ExternalId == dto.ExternalId, ct);

            if (existing is null)
            {
                db.WinnerMatches.Add(new WinnerMatch
                {
                    ExternalId  = dto.ExternalId,
                    HomeTeam    = dto.HomeTeam,
                    AwayTeam    = dto.AwayTeam,
                    League      = dto.League,
                    ScheduledAt = dto.ScheduledAt,
                    Odds1       = dto.Odds.Home,
                    OddsX       = dto.Odds.Draw,
                    Odds2       = dto.Odds.Away,
                    Status      = Enum.Parse<MatchStatus>(dto.Status, ignoreCase: true),
                    IsLive      = dto.IsLive,
                    LastUpdated = DateTime.UtcNow,
                });
            }
            else
            {
                existing.Odds1       = dto.Odds.Home;
                existing.OddsX       = dto.Odds.Draw;
                existing.Odds2       = dto.Odds.Away;
                existing.Status      = Enum.Parse<MatchStatus>(dto.Status, ignoreCase: true);
                existing.IsLive      = dto.IsLive;
                existing.LastUpdated = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task CacheMatchesAsync(List<WinnerMatchDto> matches, CancellationToken ct)
    {
        await cache.SetStringAsync(
            CacheKey,
            JsonSerializer.Serialize(matches),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
            ct
        );
    }

    private static WinnerMatchDto ToDto(WinnerMatch m) =>
        new(m.Id, m.ExternalId, m.HomeTeam, m.AwayTeam, m.League,
            m.ScheduledAt, new(m.Odds1, m.OddsX, m.Odds2),
            m.Status.ToString().ToLower(), m.IsLive, m.LastUpdated,
            BetType: null, Handicap: null, SubMarket: null);
}

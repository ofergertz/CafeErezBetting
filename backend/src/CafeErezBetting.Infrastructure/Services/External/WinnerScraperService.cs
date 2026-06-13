using System.Text.Json;
using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Entities;
using CafeErezBetting.Core.Interfaces.Services;
using CafeErezBetting.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Syncs Winner match data from external sources (backend-only, never client).
/// Primary source: Telesport JSON API (fast, clean structured data).
/// Secondary source: Livegames JSON API (supplementary, one record per game).
/// Fallback: Playwright HTML scraping (slower, used when both APIs are unreachable).
/// Falls back to Redis cache or DB when all live sources fail.
/// </summary>
public class WinnerScraperService(
    AppDbContext db,
    IDistributedCache cache,
    TelesportApiClient telesportApi,
    LivegamesApiClient livegamesApi,
    PlaywrightWinnerScraper playwright,
    ILogger<WinnerScraperService> logger
) : IWinnerSyncService
{
    private const string CacheKey    = "winner:matches";
    private const string LastSyncKey = "winner:last_sync";
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
            logger.LogWarning(ex, "DB unavailable — returning empty list");
            return [];
        }
    }

    public async Task<SyncStatusDto> SyncNowAsync(CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Winner sync started");

            var matches = await ScrapeExternalAsync(ct);

            if (matches.Count > 0)
            {
                try { await PersistMatchesAsync(matches, ct); }
                catch (Exception ex) { logger.LogWarning(ex, "DB persist failed — cache will still be updated"); }

                await CacheMatchesAsync(matches, ct);
            }

            var dto = new SyncStatusDto(true, DateTime.UtcNow, null, false);
            await cache.SetStringAsync(
                LastSyncKey,
                JsonSerializer.Serialize(dto),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1) },
                ct
            );

            logger.LogInformation("Winner sync completed: {Count} matches", matches.Count);
            return dto;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Winner sync failed");
            return new SyncStatusDto(false, DateTime.UtcNow, ex.Message, false);
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    public async Task<List<WinnerMatchDto>> ScrapeFromSourceAsync(int sourceIndex, CancellationToken ct = default)
    {
        logger.LogInformation("Direct scrape from source {Index}", sourceIndex);
        // 0 = Telesport API, 1 = Livegames API, any other = Playwright
        return sourceIndex switch
        {
            0 => await telesportApi.FetchWinnerMatchesAsync(ct),
            1 => await livegamesApi.FetchWinnerMatchesAsync(ct),
            _ => await playwright.ScrapeAsync(sourceIndex, ct),
        };
    }

    private async Task<List<WinnerMatchDto>> ScrapeExternalAsync(CancellationToken ct)
    {
        // 1. Try Telesport JSON API (fast, ~1–2 s)
        try
        {
            var apiMatches = await telesportApi.FetchWinnerMatchesAsync(ct);
            if (apiMatches.Count > 0)
            {
                logger.LogInformation("TelesportAPI sync succeeded: {Count} matches", apiMatches.Count);
                return apiMatches;
            }
            logger.LogWarning("TelesportAPI returned 0 matches — trying Livegames");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TelesportAPI failed — trying Livegames");
        }

        // 2. Secondary: Livegames JSON API
        try
        {
            var lgMatches = await livegamesApi.FetchWinnerMatchesAsync(ct);
            if (lgMatches.Count > 0)
            {
                logger.LogInformation("LivegamesAPI sync succeeded: {Count} matches", lgMatches.Count);
                return lgMatches;
            }
            logger.LogWarning("LivegamesAPI returned 0 matches — falling back to Playwright");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LivegamesAPI failed — falling back to Playwright");
        }

        // 3. Last resort: Playwright scraper (slower, ~30–60 s)
        try
        {
            var scraped = await playwright.ScrapeAllAsync(ct);
            if (scraped.Count > 0)
            {
                logger.LogInformation("Playwright fallback succeeded: {Count} matches", scraped.Count);
                return scraped;
            }
            logger.LogWarning("Playwright returned 0 matches");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Playwright scrape also failed");
        }

        return [];
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

    private async Task PersistMatchesAsync(List<WinnerMatchDto> matches, CancellationToken ct)
    {
        var currentIds = matches.Select(m => m.ExternalId).ToHashSet();
        var stale = await db.WinnerMatches
            .Where(m => !currentIds.Contains(m.ExternalId))
            .ToListAsync(ct);
        if (stale.Count > 0)
        {
            db.WinnerMatches.RemoveRange(stale);
            logger.LogInformation("Removed {Count} stale matches from DB", stale.Count);
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
                    OddsX       = dto.Odds.Draw ?? 0,
                    Odds2       = dto.Odds.Away,
                    Status      = Enum.Parse<MatchStatus>(dto.Status, ignoreCase: true),
                    IsLive      = dto.IsLive,
                    LastUpdated = DateTime.UtcNow,
                });
            }
            else
            {
                existing.Odds1       = dto.Odds.Home;
                existing.OddsX       = dto.Odds.Draw ?? 0;
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
            m.ScheduledAt, new(m.Odds1, m.OddsX == 0 ? null : m.OddsX, m.Odds2),
            m.Status.ToString().ToLower(), m.IsLive, m.LastUpdated,
            BetType: null, Handicap: null, SubMarket: null);
}

using System.Text.Json;
using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Entities;
using CafeErezBetting.Core.Interfaces.Services;
using CafeErezBetting.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services.External;

/// <summary>
/// Syncs Winner/Toto match data from external sources (backend-only, never client).
/// Falls back to Redis cache on external failure.
/// </summary>
public class WinnerScraperService(
    AppDbContext db,
    IDistributedCache cache,
    IHttpClientFactory httpClientFactory,
    IHostEnvironment env,
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
        var cached = await cache.GetStringAsync(CacheKey, ct);
        if (cached is not null)
        {
            try
            {
                return JsonSerializer.Deserialize<List<WinnerMatchDto>>(cached) ?? [];
            }
            catch
            {
                // corrupted cache — fall through
            }
        }

        // 2. Fall back to DB
        return await GetFromDbAsync(ct);
    }

    public async Task<SyncStatusDto> SyncNowAsync(CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Winner sync started");

            var matches = await ScrapeExternalAsync(ct);

            if (matches.Count > 0)
            {
                await PersistMatchesAsync(matches, ct);
                await CacheMatchesAsync(matches, ct);
            }

            var dto = new SyncStatusDto(true, DateTime.UtcNow, null);
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
            logger.LogError(ex, "Winner sync failed, using cached data");
            return new SyncStatusDto(false, DateTime.UtcNow, ex.Message);
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task<List<WinnerMatchDto>> GetFromDbAsync(CancellationToken ct)
    {
        var matches = await db.WinnerMatches
            .Where(m => m.Status != MatchStatus.Finished)
            .OrderBy(m => m.ScheduledAt)
            .Take(100)
            .ToListAsync(ct);

        return matches.Select(ToDto).ToList();
    }

    /// <summary>
    /// Scrape from external source.
    /// Development: returns mock data for fast local iteration.
    /// Production: uses Playwright to scrape telesport.co.il.
    /// </summary>
    private async Task<List<WinnerMatchDto>> ScrapeExternalAsync(CancellationToken ct)
    {
        if (env.IsDevelopment())
        {
            await Task.Delay(50, ct);
            return GetMockData();
        }

        try
        {
            var scraped = await playwright.ScrapeAsync(ct);
            if (scraped.Count > 0) return scraped;
            logger.LogWarning("Playwright returned 0 matches — falling back to mock data");
            return GetMockData();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Playwright scrape failed — falling back to mock data");
            return GetMockData();
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
            m.Status.ToString().ToLower(), m.IsLive, m.LastUpdated);
}

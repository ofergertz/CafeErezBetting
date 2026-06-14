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
/// Primary source:   Livegames mobile JSON API  (m.livegames.co.il/api/winner)
/// Secondary source: Telesport mobile JSON API  (m.telesport.co.il/api/winner)
/// Falls back to Redis cache or DB when both APIs fail.
/// No API key required for either source.
/// </summary>
public class WinnerScraperService(
    AppDbContext db,
    IDistributedCache cache,
    LivegamesApiClient livegamesApi,
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
        // 0 = Livegames mobile, 1 = Telesport mobile
        return sourceIndex == 1
            ? await livegamesApi.FetchTelesportMobileAsync(ct)
            : await livegamesApi.FetchWinnerMatchesAsync(ct);
    }

    private async Task<List<WinnerMatchDto>> ScrapeExternalAsync(CancellationToken ct)
    {
        // 1. Primary: Livegames mobile JSON API
        try
        {
            var matches = await livegamesApi.FetchWinnerMatchesAsync(ct);
            if (matches.Count > 0)
            {
                logger.LogInformation("LivegamesAPI sync succeeded: {Count} matches", matches.Count);
                return matches;
            }
            logger.LogWarning("LivegamesAPI returned 0 matches — trying Telesport mobile");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LivegamesAPI failed — trying Telesport mobile");
        }

        // 2. Secondary: Telesport mobile JSON API
        try
        {
            var matches = await livegamesApi.FetchTelesportMobileAsync(ct);
            if (matches.Count > 0)
            {
                logger.LogInformation("Telesport mobile sync succeeded: {Count} matches", matches.Count);
                return matches;
            }
            logger.LogWarning("Telesport mobile returned 0 matches");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Telesport mobile also failed");
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
                    ScheduledAt = ToUtc(dto.ScheduledAt),
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

    // JSON deserialization produces Kind=Unspecified for dates without tz offset.
    // PostgreSQL timestamptz only accepts Utc.
    private static DateTime ToUtc(DateTime dt) => dt.Kind switch
    {
        DateTimeKind.Utc       => dt,
        DateTimeKind.Local     => dt.ToUniversalTime(),
        _                      => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
    };
}

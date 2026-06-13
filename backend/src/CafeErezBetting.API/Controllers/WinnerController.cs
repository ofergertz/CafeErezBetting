using System.Text.Json;
using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace CafeErezBetting.API.Controllers;

[ApiController]
[Route("api/winner")]
public class WinnerController(
    IWinnerSyncService syncService,
    IDistributedCache cache,
    IConfiguration config
) : ControllerBase
{
    [HttpGet("sources")]
    public IActionResult GetSources()
    {
        var urls = config.GetSection("Scrapers:Winner:Urls").Get<string[]>() ?? [];
        var sources = urls.Select((url, i) => new { index = i, name = $"מקור {i + 1}", url }).ToArray();
        return Ok(sources);
    }

    [HttpGet("matches")]
    public async Task<IActionResult> GetMatches([FromQuery] int? source, CancellationToken ct)
    {
        if (source.HasValue)
        {
            var scraped = await syncService.ScrapeFromSourceAsync(source.Value, ct);
            if (scraped.Count > 0) return Ok(scraped);
            // Source returned empty — fall back to cache/DB so the UI never shows a blank screen
            var cached = await syncService.GetMatchesAsync(ct);
            return Ok(cached);
        }
        var matches = await syncService.GetMatchesAsync(ct);
        return Ok(matches);
    }

    [HttpPost("sync")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ManualSync(CancellationToken ct)
    {
        var result = await syncService.SyncNowAsync(ct);
        return Ok(result);
    }

    [HttpGet("sync-status")]
    public async Task<IActionResult> GetSyncStatus(CancellationToken ct)
    {
        var json = await cache.GetStringAsync("winner:last_sync", ct);
        if (json is null)
            return Ok(new { lastSync = (DateTime?)null, success = false, isMock = true, error = (string?)null });

        try
        {
            var dto = JsonSerializer.Deserialize<SyncStatusDto>(json);
            return Ok(new
            {
                lastSync = dto?.LastSync,
                success  = dto?.Success ?? false,
                isMock   = dto?.IsMock  ?? true,
                error    = dto?.Error,
            });
        }
        catch
        {
            return Ok(new { lastSync = (DateTime?)null, success = false, isMock = true, error = (string?)null });
        }
    }
}

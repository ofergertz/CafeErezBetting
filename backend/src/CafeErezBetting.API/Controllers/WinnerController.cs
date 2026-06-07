using System.Text.Json;
using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace CafeErezBetting.API.Controllers;

[ApiController]
[Route("api/winner")]
public class WinnerController(IWinnerSyncService syncService, IDistributedCache cache) : ControllerBase
{
    [HttpGet("matches")]
    public async Task<IActionResult> GetMatches(CancellationToken ct)
    {
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
            return Ok(new { lastSync = (DateTime?)null, success = false });

        try
        {
            var dto = JsonSerializer.Deserialize<SyncStatusDto>(json);
            return Ok(new { lastSync = dto?.LastSync, success = dto?.Success ?? false });
        }
        catch
        {
            return Ok(new { lastSync = (DateTime?)null, success = false });
        }
    }
}

using CafeErezBetting.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeErezBetting.API.Controllers;

[ApiController]
[Route("api/winner")]
public class WinnerController(IWinnerSyncService syncService) : ControllerBase
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
}

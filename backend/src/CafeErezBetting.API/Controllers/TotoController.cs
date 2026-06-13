using CafeErezBetting.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace CafeErezBetting.API.Controllers;

[ApiController]
[Route("api/toto")]
public class TotoController(ITotoSyncService totoSync) : ControllerBase
{
    // GET /api/toto/current — returns the current toto round with matches
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentRound(CancellationToken ct)
    {
        var round = await totoSync.GetCurrentRoundAsync(ct);
        return round is null ? NotFound() : Ok(round);
    }

    // POST /api/toto/sync (admin) — manual refresh
    [HttpPost("sync")]
    public async Task<IActionResult> ManualSync(CancellationToken ct)
    {
        var status = await totoSync.SyncNowAsync(ct);
        return Ok(status);
    }
}

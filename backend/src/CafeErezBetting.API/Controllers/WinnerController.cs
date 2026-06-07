using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeErezBetting.API.Controllers;

[ApiController]
[Route("api/winner")]
public class WinnerController : ControllerBase
{
    // GET /api/winner/matches
    [HttpGet("matches")]
    public IActionResult GetMatches()
    {
        // TODO: Phase 2 — return from Redis cache, fallback to DB
        return Ok(new { matches = Array.Empty<object>() });
    }

    // POST /api/winner/sync  (admin only)
    [HttpPost("sync")]
    [Authorize(Roles = "admin")]
    public IActionResult ManualSync()
    {
        // TODO: Phase 2 — trigger Winner/Toto sync from Telesport/Livegames
        return Ok(new { message = "Sync triggered" });
    }
}

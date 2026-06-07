using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeErezBetting.API.Controllers;

[ApiController]
[Route("api/forms")]
public class FormsController : ControllerBase
{
    // POST /api/forms/winner
    [HttpPost("winner")]
    public IActionResult SubmitWinner([FromBody] object payload)
    {
        // TODO: Phase 2 — save form, emit SignalR event to admin
        return Ok(new { id = Guid.NewGuid() });
    }

    // POST /api/forms/toto
    [HttpPost("toto")]
    public IActionResult SubmitToto([FromBody] object payload)
    {
        // TODO: Phase 3
        return Ok(new { id = Guid.NewGuid() });
    }

    // POST /api/forms/lotto
    [HttpPost("lotto")]
    public IActionResult SubmitLotto([FromBody] object payload)
    {
        // TODO: Phase 3
        return Ok(new { id = Guid.NewGuid() });
    }

    // POST /api/forms/chance
    [HttpPost("chance")]
    public IActionResult SubmitChance([FromBody] object payload)
    {
        // TODO: Phase 3
        return Ok(new { id = Guid.NewGuid() });
    }

    // POST /api/forms/777
    [HttpPost("777")]
    public IActionResult Submit777([FromBody] object payload)
    {
        // TODO: Phase 3
        return Ok(new { id = Guid.NewGuid() });
    }

    // GET /api/forms  (admin only)
    [HttpGet]
    [Authorize(Roles = "admin")]
    public IActionResult GetForms([FromQuery] string? status, [FromQuery] string? type, [FromQuery] DateOnly? date)
    {
        // TODO: Phase 4
        return Ok(new { forms = Array.Empty<object>() });
    }

    // PATCH /api/forms/{id}/status  (admin only)
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "admin")]
    public IActionResult UpdateStatus(Guid id, [FromBody] UpdateStatusRequest req)
    {
        // TODO: Phase 4 — update status + emit SignalR event
        return Ok();
    }
}

public record UpdateStatusRequest(string Status);

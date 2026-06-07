using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Entities;
using CafeErezBetting.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeErezBetting.API.Controllers;

[ApiController]
[Route("api/forms")]
public class FormsController(IFormsService formsService) : ControllerBase
{
    [HttpPost("winner")]
    public async Task<IActionResult> SubmitWinner([FromBody] SubmitWinnerFormDto dto, CancellationToken ct)
    {
        var result = await formsService.SubmitWinnerFormAsync(dto, ct);
        return Ok(result);
    }

    [HttpPost("toto")]
    public IActionResult SubmitToto([FromBody] object payload) =>
        Ok(new { id = Guid.NewGuid() }); // TODO: Phase 3

    [HttpPost("lotto")]
    public IActionResult SubmitLotto([FromBody] object payload) =>
        Ok(new { id = Guid.NewGuid() }); // TODO: Phase 3

    [HttpPost("chance")]
    public IActionResult SubmitChance([FromBody] object payload) =>
        Ok(new { id = Guid.NewGuid() }); // TODO: Phase 3

    [HttpPost("777")]
    public IActionResult Submit777([FromBody] object payload) =>
        Ok(new { id = Guid.NewGuid() }); // TODO: Phase 3

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetForms(
        [FromQuery] string? status,
        [FromQuery] string? type,
        [FromQuery] DateOnly? date,
        CancellationToken ct)
    {
        var forms = await formsService.GetAllFormsAsync(status, type, date, ct);
        return Ok(forms);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest req, CancellationToken ct)
    {
        if (!Enum.TryParse<FormStatus>(req.Status, true, out var status))
            return BadRequest(new { message = $"Invalid status: {req.Status}" });

        await formsService.UpdateFormStatusAsync(id, status, ct);
        return Ok();
    }
}

public record UpdateStatusRequest(string Status);

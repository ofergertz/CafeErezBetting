using System.Text.Json;
using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Entities;
using CafeErezBetting.Core.Interfaces.Services;
using CafeErezBetting.Core.Services;
using CafeErezBetting.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeErezBetting.API.Controllers;

[ApiController]
[Route("api/forms")]
public class FormsController(IFormsService formsService, AppDbContext db, IMatchNotificationService notifier) : ControllerBase
{
    // ─── Winner ───────────────────────────────────────────────────────────────

    [HttpPost("winner")]
    [Authorize]
    public async Task<IActionResult> SubmitWinner([FromBody] SubmitWinnerFormDto dto, CancellationToken ct)
    {
        var result = await formsService.SubmitWinnerFormAsync(dto, ct);
        return Ok(result);
    }

    // ─── Toto ─────────────────────────────────────────────────────────────────

    [HttpPost("toto")]
    [Authorize]
    public async Task<IActionResult> SubmitToto([FromBody] SubmitTotoFormDto dto, CancellationToken ct)
    {
        try
        {
            // validate: column count and all picks present
            LotteryValidationService.ValidateToto(dto, expectedMatchCount: dto.Columns[0].Picks.Count);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var form = new BettingForm
        {
            Type       = FormType.Toto,
            CustomerId = dto.CustomerId,
            Payload    = JsonDocument.Parse(JsonSerializer.Serialize(dto)),
            Status     = FormStatus.Received,
        };

        db.BettingForms.Add(form);
        await db.SaveChangesAsync(ct);

        var customerName = await ResolveCustomerNameAsync(dto.CustomerId, ct);
        await notifier.NotifyNewFormAsync(form.Id, "toto", customerName, ct);

        return Ok(new FormSubmittedDto(form.Id, form.Status.ToString().ToLower()));
    }

    // ─── Lotto ────────────────────────────────────────────────────────────────

    [HttpPost("lotto")]
    [Authorize]
    public async Task<IActionResult> SubmitLotto([FromBody] SubmitLottoFormDto dto, CancellationToken ct)
    {
        try { LotteryValidationService.ValidateLotto(dto); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }

        var form = new BettingForm
        {
            Type       = FormType.Lotto,
            CustomerId = dto.CustomerId,
            Payload    = JsonDocument.Parse(JsonSerializer.Serialize(dto)),
            Status     = FormStatus.Received,
        };

        db.BettingForms.Add(form);
        await db.SaveChangesAsync(ct);

        var customerName = await ResolveCustomerNameAsync(dto.CustomerId, ct);
        await notifier.NotifyNewFormAsync(form.Id, "lotto", customerName, ct);

        return Ok(new FormSubmittedDto(form.Id, form.Status.ToString().ToLower()));
    }

    // ─── Chance ──────────────────────────────────────────────────────────────

    [HttpPost("chance")]
    [Authorize]
    public async Task<IActionResult> SubmitChance([FromBody] SubmitChanceFormDto dto, CancellationToken ct)
    {
        try { LotteryValidationService.ValidateChance(dto); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }

        var form = new BettingForm
        {
            Type       = FormType.Chance,
            CustomerId = dto.CustomerId,
            Payload    = JsonDocument.Parse(JsonSerializer.Serialize(dto)),
            Status     = FormStatus.Received,
        };

        db.BettingForms.Add(form);
        await db.SaveChangesAsync(ct);

        var customerName = await ResolveCustomerNameAsync(dto.CustomerId, ct);
        await notifier.NotifyNewFormAsync(form.Id, "chance", customerName, ct);

        return Ok(new FormSubmittedDto(form.Id, form.Status.ToString().ToLower()));
    }

    // ─── 777 ─────────────────────────────────────────────────────────────────

    [HttpPost("777")]
    [Authorize]
    public async Task<IActionResult> Submit777([FromBody] SubmitLucky777FormDto dto, CancellationToken ct)
    {
        try { LotteryValidationService.ValidateLucky777(dto); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }

        var form = new BettingForm
        {
            Type       = FormType.Lucky777,
            CustomerId = dto.CustomerId,
            Payload    = JsonDocument.Parse(JsonSerializer.Serialize(dto)),
            Status     = FormStatus.Received,
        };

        db.BettingForms.Add(form);
        await db.SaveChangesAsync(ct);

        var customerName = await ResolveCustomerNameAsync(dto.CustomerId, ct);
        await notifier.NotifyNewFormAsync(form.Id, "777", customerName, ct);

        return Ok(new FormSubmittedDto(form.Id, form.Status.ToString().ToLower()));
    }

    // ─── Admin endpoints ──────────────────────────────────────────────────────

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

        try
        {
            await formsService.UpdateFormStatusAsync(id, status, ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        return Ok();
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private async Task<string> ResolveCustomerNameAsync(Guid? customerId, CancellationToken ct)
    {
        if (!customerId.HasValue) return "אנונימי";
        var customer = await db.Customers.FindAsync([customerId.Value], ct);
        return customer is null ? "אנונימי" : $"{customer.FirstName} {customer.LastName}";
    }
}

public record UpdateStatusRequest(string Status);

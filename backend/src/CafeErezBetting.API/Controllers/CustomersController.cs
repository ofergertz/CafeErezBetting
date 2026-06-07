using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeErezBetting.API.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize(Roles = "admin")]
public class CustomersController(ICustomerService customerService) : ControllerBase
{
    // ─── Customers ────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var customers = await customerService.GetAllAsync(ct);
        return Ok(new { customers });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto, CancellationToken ct)
    {
        try
        {
            var created = await customerService.CreateAsync(dto, ct);
            return StatusCode(201, created);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerDto dto, CancellationToken ct)
    {
        try
        {
            var updated = await customerService.UpdateAsync(id, dto, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { message = e.Message });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await customerService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { message = e.Message });
        }
    }

    // ─── Debts ────────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/debts")]
    public async Task<IActionResult> GetDebts(Guid id, CancellationToken ct)
    {
        try
        {
            var debts = await customerService.GetDebtsAsync(id, ct);
            return Ok(new { debts });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { message = e.Message });
        }
    }

    [HttpPost("{id:guid}/debts")]
    public async Task<IActionResult> AddDebt(Guid id, [FromBody] CreateDebtDto dto, CancellationToken ct)
    {
        try
        {
            var debt = await customerService.AddDebtAsync(id, dto, ct);
            return StatusCode(201, debt);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { message = e.Message });
        }
    }

    [HttpPut("{id:guid}/debts/{debtId:guid}")]
    public async Task<IActionResult> UpdateDebt(Guid id, Guid debtId, [FromBody] UpdateDebtDto dto, CancellationToken ct)
    {
        try
        {
            var debt = await customerService.UpdateDebtAsync(id, debtId, dto, ct);
            return Ok(debt);
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { message = e.Message });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpDelete("{id:guid}/debts/{debtId:guid}")]
    public async Task<IActionResult> DeleteDebt(Guid id, Guid debtId, CancellationToken ct)
    {
        try
        {
            await customerService.DeleteDebtAsync(id, debtId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { message = e.Message });
        }
    }
}

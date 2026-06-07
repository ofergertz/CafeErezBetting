using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeErezBetting.API.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize(Roles = "admin")]
public class CustomersController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() =>
        Ok(new { customers = Array.Empty<object>() }); // TODO: Phase 4

    [HttpPost]
    public IActionResult Create([FromBody] object dto) =>
        Ok(); // TODO: Phase 4

    [HttpPut("{id:guid}")]
    public IActionResult Update(Guid id, [FromBody] object dto) =>
        Ok(); // TODO: Phase 4

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id) =>
        Ok(); // TODO: Phase 4

    // ─── Debts ─────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/debts")]
    public IActionResult GetDebts(Guid id) =>
        Ok(new { debts = Array.Empty<object>() }); // TODO: Phase 4

    [HttpPost("{id:guid}/debts")]
    public IActionResult AddDebt(Guid id, [FromBody] object dto) =>
        Ok(); // TODO: Phase 4

    [HttpPut("{id:guid}/debts/{debtId:guid}")]
    public IActionResult UpdateDebt(Guid id, Guid debtId, [FromBody] object dto) =>
        Ok(); // TODO: Phase 4

    [HttpDelete("{id:guid}/debts/{debtId:guid}")]
    public IActionResult DeleteDebt(Guid id, Guid debtId) =>
        Ok(); // TODO: Phase 4
}

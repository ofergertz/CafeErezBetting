using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeErezBetting.API.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() =>
        Ok(new { products = Array.Empty<object>() }); // TODO: Phase 5

    [HttpPost]
    [Authorize(Roles = "admin")]
    public IActionResult Create([FromBody] object dto) =>
        Ok(); // TODO: Phase 5

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin")]
    public IActionResult Update(Guid id, [FromBody] object dto) =>
        Ok(); // TODO: Phase 5

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public IActionResult Delete(Guid id) =>
        Ok(); // TODO: Phase 5
}

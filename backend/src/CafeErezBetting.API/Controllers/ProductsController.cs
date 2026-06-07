using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeErezBetting.API.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await productService.GetAllAsync();
        return Ok(products);
    }

    [HttpGet("barcode/{barcode}")]
    public async Task<IActionResult> GetByBarcode(string barcode)
    {
        var product = await productService.GetByBarcodeAsync(barcode);
        if (product is null) return NotFound(new { message = "Product not found." });
        return Ok(product);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
    {
        try
        {
            var product = await productService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetAll), new { id = product.Id }, product);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductDto dto)
    {
        try
        {
            var product = await productService.UpdateAsync(id, dto);
            return Ok(product);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await productService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}

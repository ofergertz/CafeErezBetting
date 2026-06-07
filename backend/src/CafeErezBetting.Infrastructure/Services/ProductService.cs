using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Entities;
using CafeErezBetting.Core.Interfaces.Services;
using CafeErezBetting.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeErezBetting.Infrastructure.Services;

public class ProductService(AppDbContext db) : IProductService
{
    private static void Validate(string name, decimal price, string? barcode = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.");
        if (price <= 0)
            throw new ArgumentException("Price must be greater than zero.");
        if (Math.Round(price, 2) != price)
            throw new ArgumentException("Price must have at most 2 decimal places.");
        if (barcode != null && barcode.Length > 50)
            throw new ArgumentException("Barcode must not exceed 50 characters.");
    }

    private static ProductDto Map(Product p) =>
        new(p.Id, p.Name, p.Description, p.Price, p.ImageUrl, p.InStock, p.CreatedAt, p.Barcode);

    public async Task<List<ProductDto>> GetAllAsync() =>
        await db.Products
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.ImageUrl, p.InStock, p.CreatedAt, p.Barcode))
            .ToListAsync();

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        Validate(dto.Name, dto.Price, dto.Barcode);
        if (!string.IsNullOrWhiteSpace(dto.Barcode))
        {
            var exists = await db.Products.AnyAsync(p => p.Barcode == dto.Barcode.Trim());
            if (exists) throw new InvalidOperationException("Barcode already in use.");
        }
        var product = new Product
        {
            Name        = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Price       = dto.Price,
            ImageUrl    = dto.ImageUrl?.Trim(),
            InStock     = dto.InStock,
            Barcode     = dto.Barcode?.Trim(),
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return Map(product);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductDto dto)
    {
        Validate(dto.Name, dto.Price, dto.Barcode);
        if (!string.IsNullOrWhiteSpace(dto.Barcode))
        {
            var exists = await db.Products.AnyAsync(p => p.Barcode == dto.Barcode.Trim() && p.Id != id);
            if (exists) throw new InvalidOperationException("Barcode already in use.");
        }
        var product = await db.Products.FindAsync(id)
            ?? throw new KeyNotFoundException($"Product {id} not found.");
        product.Name        = dto.Name.Trim();
        product.Description = dto.Description?.Trim();
        product.Price       = dto.Price;
        product.ImageUrl    = dto.ImageUrl?.Trim();
        product.InStock     = dto.InStock;
        product.Barcode     = dto.Barcode?.Trim();
        product.UpdatedAt   = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Map(product);
    }

    public async Task DeleteAsync(Guid id)
    {
        var product = await db.Products.FindAsync(id)
            ?? throw new KeyNotFoundException($"Product {id} not found.");
        db.Products.Remove(product);
        await db.SaveChangesAsync();
    }

    public async Task<ProductDto?> GetByBarcodeAsync(string barcode) =>
        await db.Products
            .Where(p => p.Barcode == barcode)
            .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.ImageUrl, p.InStock, p.CreatedAt, p.Barcode))
            .FirstOrDefaultAsync();
}

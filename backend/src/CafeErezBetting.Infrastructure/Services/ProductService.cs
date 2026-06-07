using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Entities;
using CafeErezBetting.Core.Interfaces.Services;
using CafeErezBetting.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeErezBetting.Infrastructure.Services;

public class ProductService(AppDbContext db) : IProductService
{
    private static void Validate(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.");
        if (price <= 0)
            throw new ArgumentException("Price must be greater than zero.");
        if (Math.Round(price, 2) != price)
            throw new ArgumentException("Price must have at most 2 decimal places.");
    }

    private static ProductDto Map(Product p) =>
        new(p.Id, p.Name, p.Description, p.Price, p.ImageUrl, p.InStock, p.CreatedAt);

    public async Task<List<ProductDto>> GetAllAsync() =>
        await db.Products
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.ImageUrl, p.InStock, p.CreatedAt))
            .ToListAsync();

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        Validate(dto.Name, dto.Price);
        var product = new Product
        {
            Name        = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Price       = dto.Price,
            ImageUrl    = dto.ImageUrl?.Trim(),
            InStock     = dto.InStock,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return Map(product);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductDto dto)
    {
        Validate(dto.Name, dto.Price);
        var product = await db.Products.FindAsync(id)
            ?? throw new KeyNotFoundException($"Product {id} not found.");
        product.Name        = dto.Name.Trim();
        product.Description = dto.Description?.Trim();
        product.Price       = dto.Price;
        product.ImageUrl    = dto.ImageUrl?.Trim();
        product.InStock     = dto.InStock;
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
}

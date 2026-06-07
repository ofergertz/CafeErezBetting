namespace CafeErezBetting.Core.DTOs;

public record ProductDto(Guid Id, string Name, string? Description, decimal Price, string? ImageUrl, bool InStock, DateTime CreatedAt, string? Barcode);
public record CreateProductDto(string Name, string? Description, decimal Price, string? ImageUrl, bool InStock, string? Barcode = null);
public record UpdateProductDto(string Name, string? Description, decimal Price, string? ImageUrl, bool InStock, string? Barcode = null);

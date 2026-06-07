using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Infrastructure.Services;
using CafeErezBetting.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CafeErezBetting.Tests.Unit;

public class BarcodeValidationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Theory]
    [InlineData("1234567890123")]   // EAN-13
    [InlineData("ABC123XYZ")]       // Code128 alphanumeric
    [InlineData(null)]              // null allowed
    [InlineData("")]                // empty allowed
    public async Task Create_WithValidBarcode_Succeeds(string? barcode)
    {
        using var db = CreateInMemoryDb();
        var service = new ProductService(db);
        var dto = new CreateProductDto("Test", null, 10.00m, null, true, barcode);

        var result = await service.CreateAsync(dto);

        result.Should().NotBeNull();
        result.Name.Should().Be("Test");
    }

    [Fact]
    public async Task Create_WithBarcodeLongerThan50Chars_ThrowsArgumentException()
    {
        using var db = CreateInMemoryDb();
        var service = new ProductService(db);
        var longBarcode = new string('A', 51);
        var dto = new CreateProductDto("Test", null, 10.00m, null, true, longBarcode);

        var act = async () => await service.CreateAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Barcode*50*");
    }

    [Fact]
    public async Task Create_WithDuplicateBarcode_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDb();
        var service = new ProductService(db);

        await service.CreateAsync(new CreateProductDto("Product A", null, 10.00m, null, true, "BARCODE123"));

        var act = async () => await service.CreateAsync(new CreateProductDto("Product B", null, 20.00m, null, true, "BARCODE123"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Barcode*");
    }

    [Fact]
    public async Task GetByBarcode_WithExistingBarcode_ReturnsProduct()
    {
        using var db = CreateInMemoryDb();
        var service = new ProductService(db);

        await service.CreateAsync(new CreateProductDto("Coffee", null, 12.50m, null, true, "EAN001"));

        var result = await service.GetByBarcodeAsync("EAN001");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Coffee");
        result.Barcode.Should().Be("EAN001");
    }

    [Fact]
    public async Task GetByBarcode_WithNonExistentBarcode_ReturnsNull()
    {
        using var db = CreateInMemoryDb();
        var service = new ProductService(db);

        var result = await service.GetByBarcodeAsync("NOTEXIST");

        result.Should().BeNull();
    }
}

using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Infrastructure.Services;
using CafeErezBetting.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CafeErezBetting.Tests.Unit;

public class ProductValidationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Create_WithEmptyName_ThrowsArgumentException()
    {
        using var db = CreateInMemoryDb();
        var service = new ProductService(db);
        var dto = new CreateProductDto("", null, 10.00m, null, true);

        var act = async () => await service.CreateAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*name*required*");
    }

    [Fact]
    public async Task Create_WithWhitespaceName_ThrowsArgumentException()
    {
        using var db = CreateInMemoryDb();
        var service = new ProductService(db);
        var dto = new CreateProductDto("   ", null, 10.00m, null, true);

        var act = async () => await service.CreateAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*name*required*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task Create_WithNonPositivePrice_ThrowsArgumentException(decimal price)
    {
        using var db = CreateInMemoryDb();
        var service = new ProductService(db);
        var dto = new CreateProductDto("Test Product", null, price, null, true);

        var act = async () => await service.CreateAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Price must be greater than zero*");
    }

    [Theory]
    [InlineData("10.001")]
    [InlineData("5.999")]
    public async Task Create_WithMoreThanTwoDecimalPlaces_ThrowsArgumentException(string priceStr)
    {
        using var db = CreateInMemoryDb();
        var service = new ProductService(db);
        var dto = new CreateProductDto("Test Product", null, decimal.Parse(priceStr), null, true);

        var act = async () => await service.CreateAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*2 decimal places*");
    }

    [Fact]
    public async Task Create_WithValidData_ReturnsProductDto()
    {
        using var db = CreateInMemoryDb();
        var service = new ProductService(db);
        var dto = new CreateProductDto("Coffee", "Fresh coffee", 12.50m, null, true);

        var result = await service.CreateAsync(dto);

        result.Should().NotBeNull();
        result.Name.Should().Be("Coffee");
        result.Price.Should().Be(12.50m);
        result.InStock.Should().BeTrue();
        result.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Update_WithNonExistentId_ThrowsKeyNotFoundException()
    {
        using var db = CreateInMemoryDb();
        var service = new ProductService(db);
        var dto = new UpdateProductDto("Coffee", null, 10.00m, null, true);

        var act = async () => await service.UpdateAsync(Guid.NewGuid(), dto);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_ThrowsKeyNotFoundException()
    {
        using var db = CreateInMemoryDb();
        var service = new ProductService(db);

        var act = async () => await service.DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetAll_ReturnsProductsOrderedByName()
    {
        using var db = CreateInMemoryDb();
        var service = new ProductService(db);

        await service.CreateAsync(new CreateProductDto("Zebra Cake", null, 5.00m, null, true));
        await service.CreateAsync(new CreateProductDto("Apple Juice", null, 8.00m, null, true));
        await service.CreateAsync(new CreateProductDto("Mango Shake", null, 15.00m, null, false));

        var results = await service.GetAllAsync();

        results.Select(p => p.Name).Should().BeInAscendingOrder();
        results[0].Name.Should().Be("Apple Juice");
        results[1].Name.Should().Be("Mango Shake");
        results[2].Name.Should().Be("Zebra Cake");
    }
}

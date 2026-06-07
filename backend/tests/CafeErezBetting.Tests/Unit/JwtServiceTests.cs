using CafeErezBetting.Core.Entities;
using CafeErezBetting.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CafeErezBetting.Tests.Unit;

public class JwtServiceTests
{
    private readonly JwtService _sut;

    public JwtServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-must-be-at-least-32-chars-long!",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();
        _sut = new JwtService(config);
    }

    [Fact]
    public void GenerateAdminToken_ReturnsNonEmptyToken()
    {
        var user = new AdminUser { Id = Guid.NewGuid(), Username = "admin", DisplayName = "Admin" };
        var token = _sut.GenerateAdminToken(user);
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateCustomerToken_ReturnsNonEmptyToken()
    {
        var customer = new Customer { Id = Guid.NewGuid(), Phone = "0501234567" };
        var token = _sut.GenerateCustomerToken(customer);
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ValidateToken_AdminToken_ReturnsAdminRole()
    {
        var user = new AdminUser { Id = Guid.NewGuid(), Username = "admin", DisplayName = "Admin" };
        var token = _sut.GenerateAdminToken(user);
        var result = _sut.ValidateToken(token);
        result.Should().NotBeNull();
        result!.Value.role.Should().Be("admin");
        result.Value.userId.Should().Be(user.Id.ToString());
    }

    [Fact]
    public void ValidateToken_CustomerToken_ReturnsCustomerRole()
    {
        var customer = new Customer { Id = Guid.NewGuid(), Phone = "0501234567" };
        var token = _sut.GenerateCustomerToken(customer);
        var result = _sut.ValidateToken(token);
        result.Should().NotBeNull();
        result!.Value.role.Should().Be("customer");
        result.Value.userId.Should().Be(customer.Id.ToString());
    }

    [Fact]
    public void ValidateToken_InvalidToken_ReturnsNull()
    {
        var result = _sut.ValidateToken("not.a.valid.jwt.token");
        result.Should().BeNull();
    }
}

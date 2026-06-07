using CafeErezBetting.Core.Entities;

namespace CafeErezBetting.Core.Interfaces.Services;

public interface IJwtService
{
    string GenerateAdminToken(AdminUser user);
    string GenerateCustomerToken(Customer customer);
    (string userId, string role)? ValidateToken(string token);
}

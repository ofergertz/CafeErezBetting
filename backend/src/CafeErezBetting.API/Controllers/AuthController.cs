using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Text.RegularExpressions;
using CafeErezBetting.Core.Entities;
using CafeErezBetting.Core.Interfaces.Services;
using CafeErezBetting.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using BC = BCrypt.Net.BCrypt;

namespace CafeErezBetting.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly IOtpService _otp;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AuthController> _logger;

    private static readonly Regex IsraeliPhoneRegex =
        new(@"^0(50|52|53|54|55|58|2|3|4|8|9)\d{7}$", RegexOptions.Compiled);

    public AuthController(
        AppDbContext db,
        IJwtService jwt,
        IOtpService otp,
        IDistributedCache cache,
        ILogger<AuthController> logger)
    {
        _db = db;
        _jwt = jwt;
        _otp = otp;
        _cache = cache;
        _logger = logger;
    }

    // POST /api/auth/admin/login
    [HttpPost("admin/login")]
    public async Task<IActionResult> AdminLogin([FromBody] AdminLoginRequest req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var rlKey = $"rl:admin:{ip}";
        var rlVal = await _cache.GetStringAsync(rlKey);
        if (rlVal is not null && int.TryParse(rlVal, out var attempts) && attempts >= 5)
            return StatusCode(429, new { message = "auth.rateLimited" });

        var user = await _db.AdminUsers
            .FirstOrDefaultAsync(u => u.Username.ToLower() == req.Username.ToLower() && u.IsActive);

        if (user is null || !BC.Verify(req.Password, user.PasswordHash))
        {
            // Increment IP-based rate limit counter (60s window)
            var currentVal = await _cache.GetStringAsync(rlKey);
            var count = currentVal is null ? 1 : int.Parse(currentVal) + 1;
            await _cache.SetStringAsync(rlKey, count.ToString(), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
            });
            return Unauthorized(new { message = "auth.invalidCredentials" });
        }

        // Successful login: clear rate limit counter
        await _cache.RemoveAsync(rlKey);

        var token = _jwt.GenerateAdminToken(user);

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id.ToString(),
            Role = "admin",
            Action = "admin.login",
            Payload = JsonDocument.Parse(JsonSerializer.Serialize(new { username = user.Username })),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
        });
        await _db.SaveChangesAsync();

        return Ok(new
        {
            token,
            user = new { id = user.Id, role = "admin", name = user.DisplayName }
        });
    }

    // POST /api/auth/otp/send
    [HttpPost("otp/send")]
    public async Task<IActionResult> SendOtp([FromBody] OtpSendRequest req)
    {
        var phone = req.Phone.Replace("-", "").Replace(" ", "");

        if (!IsraeliPhoneRegex.IsMatch(phone))
            return BadRequest(new { message = "auth.invalidPhone" });

        if (await _otp.IsRateLimitedAsync(phone))
            return StatusCode(429, new { message = "auth.rateLimited" });

        await _otp.SendOtpAsync(phone);

        return Ok(new { message = "sent" });
    }

    // POST /api/auth/otp/verify
    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyRequest req)
    {
        var phone = req.Phone.Replace("-", "").Replace(" ", "");

        if (req.Code.Length != 6 || !req.Code.All(char.IsDigit))
            return Unauthorized(new { message = "auth.invalidOtp" });

        var valid = await _otp.VerifyOtpAsync(phone, req.Code);
        if (!valid)
            return Unauthorized(new { message = "auth.invalidOtp" });

        // Find or create customer
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == phone);
        if (customer is null)
        {
            customer = new Customer
            {
                Phone = phone,
                FirstName = "",
                LastName = "",
                IdNumber = "",
            };
            _db.Customers.Add(customer);
        }

        var token = _jwt.GenerateCustomerToken(customer);

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = customer.Id.ToString(),
            Role = "customer",
            Action = "customer.otp.verify",
            Payload = JsonDocument.Parse(JsonSerializer.Serialize(new { phone })),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
        });
        await _db.SaveChangesAsync();

        return Ok(new
        {
            token,
            user = new { id = customer.Id, role = "customer", phone = customer.Phone }
        });
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (authHeader is null || !authHeader.StartsWith("Bearer "))
            return Ok();

        var token = authHeader["Bearer ".Length..];

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var jti = jwtToken.Id;
            var exp = jwtToken.ValidTo;
            var ttl = exp - DateTime.UtcNow;

            if (ttl > TimeSpan.Zero)
            {
                await _cache.SetStringAsync($"jwt:bl:{jti}", "1", new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to blacklist JWT on logout");
        }

        return Ok();
    }
}

public record AdminLoginRequest(string Username, string Password);
public record OtpSendRequest(string Phone);
public record OtpVerifyRequest(string Phone, string Code);

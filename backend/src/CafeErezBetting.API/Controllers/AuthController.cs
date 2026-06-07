using Microsoft.AspNetCore.Mvc;

namespace CafeErezBetting.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    // POST /api/auth/admin/login
    [HttpPost("admin/login")]
    public IActionResult AdminLogin([FromBody] AdminLoginRequest req)
    {
        // TODO: Phase 1 — implement with AdminUser entity + BCrypt + JWT
        return Ok(new { message = "Not implemented yet" });
    }

    // POST /api/auth/otp/send
    [HttpPost("otp/send")]
    public IActionResult SendOtp([FromBody] OtpSendRequest req)
    {
        // TODO: Phase 1 — generate 6-digit code, store in Redis (5min TTL), send SMS
        return Ok(new { message = "OTP sent" });
    }

    // POST /api/auth/otp/verify
    [HttpPost("otp/verify")]
    public IActionResult VerifyOtp([FromBody] OtpVerifyRequest req)
    {
        // TODO: Phase 1 — verify code from Redis, mark used, return JWT
        return Ok(new { message = "Not implemented yet" });
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // TODO: Phase 1 — blacklist JWT in Redis
        return Ok();
    }
}

public record AdminLoginRequest(string Username, string Password);
public record OtpSendRequest(string Phone);
public record OtpVerifyRequest(string Phone, string Code);

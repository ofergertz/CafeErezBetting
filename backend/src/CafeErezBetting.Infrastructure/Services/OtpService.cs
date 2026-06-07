using System.Text.Json;
using CafeErezBetting.Core.Interfaces.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using BC = BCrypt.Net.BCrypt;

namespace CafeErezBetting.Infrastructure.Services;

public class OtpService : IOtpService
{
    private readonly IDistributedCache _cache;
    private readonly ISmsService _sms;
    private readonly ILogger<OtpService> _logger;

    private const int OtpTtlSeconds = 300;       // 5 minutes
    private const int RateLimitTtlSeconds = 600;  // 10 minutes
    private const int MaxSendsPerWindow = 3;
    private const int MaxVerifyAttempts = 5;
    private const int VerifyLockoutSeconds = 600; // 10 minutes

    public OtpService(IDistributedCache cache, ISmsService sms, ILogger<OtpService> logger)
    {
        _cache = cache;
        _sms = sms;
        _logger = logger;
    }

    public async Task<bool> SendOtpAsync(string phone)
    {
        var code = GenerateCode();
        var codeHash = BC.HashPassword(code);

        var session = new OtpSession { CodeHash = codeHash, UsedAt = null };
        var json = JsonSerializer.Serialize(session);

        await _cache.SetStringAsync($"otp:{phone}", json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(OtpTtlSeconds)
        });

        // Increment rate limit counter
        var rlKey = $"otp:rl:{phone}";
        var rlVal = await _cache.GetStringAsync(rlKey);
        var count = rlVal is null ? 1 : int.Parse(rlVal) + 1;
        await _cache.SetStringAsync(rlKey, count.ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(RateLimitTtlSeconds)
        });

        await _sms.SendAsync(phone, $"קוד האימות שלך הוא: {code}");
        return true;
    }

    public async Task<bool> VerifyOtpAsync(string phone, string code)
    {
        var attemptsKey = $"otp:attempts:{phone}";

        // Check if locked out
        var attemptsVal = await _cache.GetStringAsync(attemptsKey);
        if (attemptsVal is not null && int.TryParse(attemptsVal, out var attempts) && attempts >= MaxVerifyAttempts)
            return false;

        var json = await _cache.GetStringAsync($"otp:{phone}");
        if (json is null)
        {
            await IncrementAttemptsAsync(attemptsKey);
            return false;
        }

        var session = JsonSerializer.Deserialize<OtpSession>(json);
        if (session is null)
        {
            await IncrementAttemptsAsync(attemptsKey);
            return false;
        }
        if (session.UsedAt is not null)
        {
            await IncrementAttemptsAsync(attemptsKey);
            return false;  // already used
        }

        if (!BC.Verify(code, session.CodeHash))
        {
            await IncrementAttemptsAsync(attemptsKey);
            return false;
        }

        // Success: mark as used and reset attempt counter
        session.UsedAt = DateTime.UtcNow;
        var updated = JsonSerializer.Serialize(session);
        await _cache.SetStringAsync($"otp:{phone}", updated, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(OtpTtlSeconds)
        });
        await _cache.RemoveAsync(attemptsKey);

        return true;
    }

    private async Task IncrementAttemptsAsync(string attemptsKey)
    {
        var val = await _cache.GetStringAsync(attemptsKey);
        var count = val is null ? 1 : int.Parse(val) + 1;
        await _cache.SetStringAsync(attemptsKey, count.ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(VerifyLockoutSeconds)
        });
    }

    public async Task<bool> IsRateLimitedAsync(string phone)
    {
        var val = await _cache.GetStringAsync($"otp:rl:{phone}");
        if (val is null) return false;
        return int.TryParse(val, out var count) && count >= MaxSendsPerWindow;
    }

    private static string GenerateCode()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }
}

internal class OtpSession
{
    public string CodeHash { get; set; } = string.Empty;
    public DateTime? UsedAt { get; set; }
}

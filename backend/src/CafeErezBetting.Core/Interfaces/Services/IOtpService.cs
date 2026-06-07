namespace CafeErezBetting.Core.Interfaces.Services;

public interface IOtpService
{
    Task<bool> SendOtpAsync(string phone);
    Task<bool> VerifyOtpAsync(string phone, string code);
    Task<bool> IsRateLimitedAsync(string phone);
}

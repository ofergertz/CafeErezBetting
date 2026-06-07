using CafeErezBetting.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services;

public class SmsService : ISmsService
{
    private readonly ILogger<SmsService> _logger;
    private readonly string _provider;
    private readonly bool _isDevelopment;

    public SmsService(ILogger<SmsService> logger, IConfiguration config, IHostEnvironment env)
    {
        _logger = logger;
        _provider = config["Sms:Provider"] ?? "log";
        _isDevelopment = env.IsDevelopment();
    }

    public Task SendAsync(string phone, string message)
    {
        if (_isDevelopment)
        {
            _logger.LogInformation("[SMS-DEV] To={Phone} Message={Message}", phone, message);
            return Task.CompletedTask;
        }

        // TODO: Production SMS integration (Inforu / Twilio)
        // Config keys: Sms:Provider, Sms:ApiKey, Sms:SenderId
        _logger.LogWarning("[SMS] Production SMS not yet integrated. Provider={Provider}", _provider);
        return Task.CompletedTask;
    }
}

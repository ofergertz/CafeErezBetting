using System.Net.Http;
using System.Text;
using CafeErezBetting.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.Services;

public class SmsService : ISmsService
{
    private readonly ILogger<SmsService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _provider;
    private readonly string? _username;
    private readonly string? _apiKey;
    private readonly string _senderId;
    private readonly bool _isDevelopment;

    private const string InforuEndpoint = "https://api.inforu.co.il/SendMessageXml.ashx";

    public SmsService(
        ILogger<SmsService> logger,
        IConfiguration config,
        IHostEnvironment env,
        IHttpClientFactory httpClientFactory)
    {
        _logger           = logger;
        _httpClientFactory = httpClientFactory;
        _provider         = config["Sms:Provider"] ?? "log";
        _username         = config["Sms:Username"];
        _apiKey           = config["Sms:ApiKey"];
        _senderId         = config["Sms:SenderId"] ?? "CafeErez";
        _isDevelopment    = env.IsDevelopment();
    }

    public async Task SendAsync(string phone, string message)
    {
        if (_isDevelopment)
        {
            _logger.LogInformation("[SMS-DEV] To={Phone} Message={Message}", phone, message);
            return;
        }

        if (_provider == "inforu")
        {
            await SendViaInforuAsync(phone, message);
            return;
        }

        _logger.LogWarning("[SMS] Provider '{Provider}' not configured. Message to {Phone} not sent.", _provider, phone);
    }

    private async Task SendViaInforuAsync(string phone, string message)
    {
        if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("[SMS-Inforu] Missing Sms:Username or Sms:ApiKey configuration.");
            return;
        }

        var normalizedPhone = NormalizeIsraeliPhone(phone);

        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Inforu>
              <User>
                <Username>{EscapeXml(_username)}</Username>
                <ApiKey>{EscapeXml(_apiKey)}</ApiKey>
              </User>
              <Content Type="sms">
                <Message>{EscapeXml(message)}</Message>
              </Content>
              <Recipients>
                <PhoneNumber>{normalizedPhone}</PhoneNumber>
              </Recipients>
              <Settings>
                <Sender>{EscapeXml(_senderId)}</Sender>
                <SendingTime>Immediate</SendingTime>
              </Settings>
            </Inforu>
            """;

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var content = new FormUrlEncodedContent(
                [new KeyValuePair<string, string>("InforuXML", xml)]);

            var response = await client.PostAsync(InforuEndpoint, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[SMS-Inforu] HTTP {Status} sending to {Phone}: {Body}",
                    (int)response.StatusCode, phone, body);
                return;
            }

            // Inforu returns XML; a successful send contains "<Status>1</Status>"
            if (body.Contains("<Status>1</Status>", StringComparison.OrdinalIgnoreCase))
                _logger.LogInformation("[SMS-Inforu] Sent to {Phone}", phone);
            else
                _logger.LogWarning("[SMS-Inforu] Unexpected response for {Phone}: {Body}", phone, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SMS-Inforu] Exception sending to {Phone}", phone);
        }
    }

    private static string NormalizeIsraeliPhone(string phone)
    {
        // Strip all non-digit characters
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        // 0501234567 → 0501234567 (Inforu accepts leading-zero local format)
        // Already has 972 prefix → keep as is
        return digits;
    }

    private static string EscapeXml(string value) =>
        value
            .Replace("&",  "&amp;")
            .Replace("<",  "&lt;")
            .Replace(">",  "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'",  "&apos;");
}

using CafeErezBetting.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.BackgroundServices;

/// <summary>
/// Runs Toto sync on a configurable interval (Scrapers:Toto:IntervalSeconds, default 300s = 5 min).
/// Toto rounds change weekly so a longer interval is appropriate.
/// </summary>
public class TotoSyncHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<TotoSyncHostedService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSecs = config.GetValue<int>("Scrapers:Toto:IntervalSeconds", 300);
        var interval     = TimeSpan.FromSeconds(intervalSecs);

        logger.LogInformation("TotoSyncHostedService started (interval={Secs}s)", intervalSecs);

        // Initial sync on startup
        await RunSyncAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(interval, stoppingToken);
            await RunSyncAsync(stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        try
        {
            using var scope   = scopeFactory.CreateScope();
            var syncService   = scope.ServiceProvider.GetRequiredService<ITotoSyncService>();

            var result = await syncService.SyncNowAsync(ct);

            if (result.Success)
                logger.LogDebug("Toto sync OK (mock={IsMock})", result.IsMock);
            else
                logger.LogWarning("Toto sync failed: {Error}", result.Error);
        }
        catch (OperationCanceledException)
        {
            // shutting down — normal
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in TotoSyncHostedService");
        }
    }
}

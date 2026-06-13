using CafeErezBetting.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.BackgroundServices;

/// <summary>
/// Runs Winner sync on a configurable interval (Scrapers:Winner:IntervalSeconds, default 60s) with automatic retry on failure.
/// </summary>
public class WinnerSyncHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<WinnerSyncHostedService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSecs = config.GetValue<int>("Scrapers:Winner:IntervalSeconds", 60);
        var Interval = TimeSpan.FromSeconds(intervalSecs);

        logger.LogInformation("WinnerSyncHostedService started (interval={Secs}s)", intervalSecs);

        // Initial sync on startup
        await RunSyncAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);
            await RunSyncAsync(stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        try
        {
            using var scope   = scopeFactory.CreateScope();
            var syncService   = scope.ServiceProvider.GetRequiredService<IWinnerSyncService>();
            var notifyService = scope.ServiceProvider.GetRequiredService<IMatchNotificationService>();

            var result = await syncService.SyncNowAsync(ct);

            if (result.Success)
            {
                var matches = await syncService.GetMatchesAsync(ct);
                await notifyService.BroadcastMatchUpdateAsync(matches, ct);
                logger.LogDebug("Winner sync OK, broadcast {Count} matches", matches.Count);
            }
            else
            {
                logger.LogWarning("Winner sync failed: {Error}", result.Error);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down — normal
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in WinnerSyncHostedService");
        }
    }
}

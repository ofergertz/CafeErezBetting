using CafeErezBetting.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CafeErezBetting.Infrastructure.BackgroundServices;

/// <summary>
/// Runs Winner sync every 60 seconds with automatic retry on failure.
/// </summary>
public class WinnerSyncHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<WinnerSyncHostedService> logger
) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WinnerSyncHostedService started");

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

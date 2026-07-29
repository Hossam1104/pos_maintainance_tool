namespace PosAdminTool.Agent.Services;

/// <summary>Server-side five-second polling replaces the retained WinUI UI-thread timer.</summary>
public sealed class ServicePollingWorker(ServiceMonitor monitor, ILogger<ServicePollingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try { while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) await monitor.RefreshAsync(stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogWarning(ex, "Service polling stopped unexpectedly."); }
    }
}

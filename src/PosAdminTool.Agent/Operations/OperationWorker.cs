namespace PosAdminTool.Agent.Operations;

/// <summary>Runs queued work outside request lifetime. The sole diagnostic operation is deliberately development-only.</summary>
public sealed class OperationWorker(OperationRegistry registry, ResourceLockSet locks, Audit.OperationAuditWriter audit, IHostEnvironment environment, ILogger<OperationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var entry in registry.ReadAllAsync(stoppingToken))
        {
            if (entry.State == Contracts.V1.Operations.OperationState.Cancelled) { registry.Publish(entry); continue; }
            try
            {
                using var heldLocks = await locks.AcquireAsync(entry.Locks, entry.Token).ConfigureAwait(false);
                entry.Start(); registry.Publish(entry);
                if (!environment.IsDevelopment() || entry.Type is not ("diagnostic" or "diagnostic-destructive")) { entry.Complete(Contracts.V1.Operations.OperationState.Failed); registry.Publish(entry); continue; }
                entry.Report(25, "checking", "Diagnostic checks are running."); registry.Publish(entry);
                await Task.Delay(75, entry.Token).ConfigureAwait(false);
                entry.Report(75, "verifying", "Diagnostic checks are completing."); registry.Publish(entry);
                await Task.Delay(75, entry.Token).ConfigureAwait(false);
                entry.Complete(Contracts.V1.Operations.OperationState.Succeeded); if (entry.IsDestructive) await audit.AppendAsync(entry, stoppingToken).ConfigureAwait(false); registry.Publish(entry);
            }
            catch (OperationCanceledException) { entry.Complete(Contracts.V1.Operations.OperationState.Cancelled); registry.Publish(entry); }
            catch (Exception ex) { logger.LogWarning(ex, "Operation {OperationId} failed.", entry.Id); entry.Complete(Contracts.V1.Operations.OperationState.Failed); registry.Publish(entry); }
        }
    }
}

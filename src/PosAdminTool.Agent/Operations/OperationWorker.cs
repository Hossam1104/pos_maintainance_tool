using PosAdminTool.Agent.Artifacts;
using PosAdminTool.Agent.Audit;
using PosAdminTool.Application.Services;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Operations;
using PosAdminTool.Domain.Enums;

namespace PosAdminTool.Agent.Operations;

/// <summary>Runs queued Agent work outside the request lifetime.</summary>
public sealed class OperationWorker(
    OperationRegistry registry,
    ResourceLockSet locks,
    OperationAuditWriter audit,
    BackupService backupService,
    ArtifactCatalog artifacts,
    IHostEnvironment environment,
    ILogger<OperationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var entry in registry.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                await ExecuteEntryAsync(entry, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The finally block marks work that was still queued or waiting for a lock as
            // cancelled, so no active entry is left looking runnable after worker shutdown.
        }
        finally
        {
            foreach (var entry in registry.CancelActive())
            {
                await AppendAuditSafeAsync(entry).ConfigureAwait(false);
                registry.Publish(entry);
            }
        }
    }

    private async Task ExecuteEntryAsync(OperationRegistry.Entry entry, CancellationToken stoppingToken)
    {
        var auditWritten = false;

        async Task WriteAuditAsync()
        {
            if (auditWritten || !entry.NeedsAudit) return;
            auditWritten = true;
            await AppendAuditSafeAsync(entry).ConfigureAwait(false);
        }

        try
        {
            if (entry.State == OperationState.Cancelled)
            {
                await WriteAuditAsync().ConfigureAwait(false);
                registry.Publish(entry);
                return;
            }

            using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(entry.Token, stoppingToken);
            var operationToken = operationCancellation.Token;
            using var heldLocks = await locks.AcquireAsync(entry.Locks, operationToken).ConfigureAwait(false);
            if (entry.State == OperationState.Cancelled || operationToken.IsCancellationRequested)
            {
                entry.Complete(OperationState.Cancelled);
                await WriteAuditAsync().ConfigureAwait(false);
                registry.Publish(entry);
                return;
            }

            if (!entry.TryStart())
            {
                await WriteAuditAsync().ConfigureAwait(false);
                registry.Publish(entry);
                return;
            }

            registry.Publish(entry);

            if (entry.Type == "backup" && entry.WorkItem is BackupOperationWorkItem workItem)
            {
                var progress = new Progress<BackupProgress>(update =>
                {
                    entry.Report(update.Percent, update.Stage, update.Message);
                    registry.Publish(entry);
                });
                var execution = await backupService.ExecuteAsync(
                    workItem.Settings,
                    workItem.ComponentIds,
                    workItem.DestinationPath,
                    progress,
                    operationToken).ConfigureAwait(false);

                if (entry.Token.IsCancellationRequested || stoppingToken.IsCancellationRequested)
                {
                    entry.Complete(OperationState.Cancelled);
                    await WriteAuditAsync().ConfigureAwait(false);
                    registry.Publish(entry);
                    return;
                }

                if (execution.Artifact is not null)
                {
                    try
                    {
                        var metadata = artifacts.Register(
                            entry.Principal,
                            execution.Artifact.DisplayName,
                            execution.Artifact.ArchivePath,
                            execution.Artifact.SizeBytes,
                            execution.Artifact.Sha256Checksum,
                            execution.Artifact.CreatedAtUtc);
                        entry.SetResultArtifacts([metadata.ArtifactId]);
                    }
                    catch (ArtifactCatalogCapacityException)
                    {
                        entry.Complete(OperationState.Failed, ErrorCodes.BackupArtifactCatalogFull);
                        await WriteAuditAsync().ConfigureAwait(false);
                        registry.Publish(entry);
                        return;
                    }
                }

                foreach (var error in execution.Operation.Errors)
                {
                    entry.Report(execution.Operation.Status == OperationStatus.PartialSuccess ? 95 : 80, "warning", error);
                }

                var state = entry.Token.IsCancellationRequested || stoppingToken.IsCancellationRequested
                    ? OperationState.Cancelled
                    : execution.Operation.Status switch
                    {
                        OperationStatus.Success => OperationState.Succeeded,
                        OperationStatus.PartialSuccess => OperationState.PartiallySucceeded,
                        OperationStatus.Cancelled => OperationState.Cancelled,
                        _ => OperationState.Failed,
                    };
                entry.Complete(state, state == OperationState.Failed ? "backup.failed" : state == OperationState.PartiallySucceeded ? "backup.partial_failure" : null);
                await WriteAuditAsync().ConfigureAwait(false);
                registry.Publish(entry);
                return;
            }

            if (!environment.IsDevelopment() || entry.Type is not ("diagnostic" or "diagnostic-destructive"))
            {
                entry.Complete(OperationState.Failed, "operation.unsupported");
                await WriteAuditAsync().ConfigureAwait(false);
                registry.Publish(entry);
                return;
            }

            entry.Report(25, "checking", "Diagnostic checks are running.");
            registry.Publish(entry);
            await Task.Delay(75, operationToken).ConfigureAwait(false);
            entry.Report(75, "verifying", "Diagnostic checks are completing.");
            registry.Publish(entry);
            await Task.Delay(75, operationToken).ConfigureAwait(false);
            entry.Complete(OperationState.Succeeded);
            await WriteAuditAsync().ConfigureAwait(false);
            registry.Publish(entry);
        }
        catch (OperationCanceledException) when (entry.Token.IsCancellationRequested || stoppingToken.IsCancellationRequested)
        {
            entry.Complete(OperationState.Cancelled);
            await WriteAuditAsync().ConfigureAwait(false);
            registry.Publish(entry);
        }
        catch
        {
            logger.LogWarning("Operation {OperationId} failed.", entry.Id);
            entry.Complete(OperationState.Failed, entry.Type == "backup" ? "backup.failed" : "operation.failed");
            await WriteAuditAsync().ConfigureAwait(false);
            registry.Publish(entry);
        }
        finally
        {
            // Completed entries must not retain backup settings, secrets, or internal staging
            // paths while their bounded status record remains available for rehydration.
            entry.ReleaseWorkItem();
        }
    }

    private async Task AppendAuditSafeAsync(OperationRegistry.Entry entry)
    {
        if (!entry.NeedsAudit) return;
        try
        {
            // Audit completion is independent of request/worker cancellation. The operation is
            // already terminal, and a cancelled shutdown token must not leave the audit gate held.
            await audit.AppendAsync(entry, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            logger.LogWarning("Audit persistence failed for operation {OperationId}.", entry.Id);
        }
    }
}

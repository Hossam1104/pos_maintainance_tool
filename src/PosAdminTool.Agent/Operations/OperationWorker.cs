using PosAdminTool.Agent.Artifacts;
using PosAdminTool.Agent.Audit;
using PosAdminTool.Application.Services;
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
        await foreach (var entry in registry.ReadAllAsync(stoppingToken))
        {
            if (entry.State == OperationState.Cancelled)
            {
                registry.Publish(entry);
                continue;
            }

            try
            {
                using var heldLocks = await locks.AcquireAsync(entry.Locks, entry.Token).ConfigureAwait(false);
                if (entry.State == OperationState.Cancelled)
                {
                    registry.Publish(entry);
                    continue;
                }

                if (!entry.TryStart())
                {
                    registry.Publish(entry);
                    continue;
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
                        entry.Token).ConfigureAwait(false);

                    if (execution.Artifact is not null)
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

                    foreach (var error in execution.Operation.Errors)
                    {
                        entry.Report(execution.Operation.Status == OperationStatus.PartialSuccess ? 95 : 80, "warning", error);
                    }

                    var state = execution.Operation.Status switch
                    {
                        OperationStatus.Success => OperationState.Succeeded,
                        OperationStatus.PartialSuccess => OperationState.PartiallySucceeded,
                        OperationStatus.Cancelled => OperationState.Cancelled,
                        _ => OperationState.Failed,
                    };
                    entry.Complete(state, state == OperationState.Failed ? "backup.failed" : state == OperationState.PartiallySucceeded ? "backup.partial_failure" : null);
                    if (entry.NeedsAudit) await audit.AppendAsync(entry, stoppingToken).ConfigureAwait(false);
                    registry.Publish(entry);
                    continue;
                }

                if (!environment.IsDevelopment() || entry.Type is not ("diagnostic" or "diagnostic-destructive"))
                {
                    entry.Complete(OperationState.Failed, "operation.unsupported");
                    registry.Publish(entry);
                    continue;
                }

                entry.Report(25, "checking", "Diagnostic checks are running.");
                registry.Publish(entry);
                await Task.Delay(75, entry.Token).ConfigureAwait(false);
                entry.Report(75, "verifying", "Diagnostic checks are completing.");
                registry.Publish(entry);
                await Task.Delay(75, entry.Token).ConfigureAwait(false);
                entry.Complete(OperationState.Succeeded);
                if (entry.NeedsAudit) await audit.AppendAsync(entry, stoppingToken).ConfigureAwait(false);
                registry.Publish(entry);
            }
            catch (OperationCanceledException)
            {
                entry.Complete(OperationState.Cancelled);
                if (entry.NeedsAudit) await audit.AppendAsync(entry, stoppingToken).ConfigureAwait(false);
                registry.Publish(entry);
            }
            catch
            {
                logger.LogWarning("Operation {OperationId} failed.", entry.Id);
                entry.Complete(OperationState.Failed, entry.Type == "backup" ? "backup.failed" : "operation.failed");
                if (entry.NeedsAudit) await audit.AppendAsync(entry, stoppingToken).ConfigureAwait(false);
                registry.Publish(entry);
            }
        }
    }
}

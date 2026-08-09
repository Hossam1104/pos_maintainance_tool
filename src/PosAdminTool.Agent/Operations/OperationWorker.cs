using PosAdminTool.Agent.Artifacts;
using PosAdminTool.Agent.Audit;
using PosAdminTool.Agent.Restore;
using PosAdminTool.Application.Maintenance;
using PosAdminTool.Application.Restore;
using PosAdminTool.Application.Services;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Downloader;
using PosAdminTool.Contracts.V1.Maintenance;
using PosAdminTool.Contracts.V1.Operations;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;
using PosAdminTool.Infrastructure.Configuration;
using System.Security.Cryptography;

namespace PosAdminTool.Agent.Operations;

/// <summary>Runs queued Agent work outside the request lifetime.</summary>
public sealed class OperationWorker(
    OperationRegistry registry,
    ResourceLockSet locks,
    OperationAuditWriter audit,
    BackupService backupService,
    DbDownloadService downloadService,
    RestoreService restoreService,
    MaintenanceService maintenanceService,
    RestoreSourceResolver restoreSourceResolver,
    ArtifactCatalog artifacts,
    IConfigurationService configuration,
    IAgentSecretStore secrets,
    AgentConfigurationStoreOptions configurationOptions,
    TimeProvider timeProvider,
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

            if (entry.Type == "downloader" && entry.WorkItem is DownloaderOperationWorkItem downloaderWorkItem)
            {
                var execution = await ExecuteDownloaderAsync(
                    entry,
                    downloaderWorkItem,
                    downloadService,
                    artifacts,
                    secrets,
                    configurationOptions,
                    timeProvider,
                    operationToken,
                    stoppingToken).ConfigureAwait(false);
                entry.SetDownloaderOutcome(execution.Outcome);
                entry.SetResultArtifacts(execution.ArtifactIds);
                entry.Complete(execution.State, execution.ErrorCode, preserveOutcomeOnCancellation: true);
                await WriteAuditAsync().ConfigureAwait(false);
                registry.Publish(entry);
                return;
            }

            if (entry.Type == "restore" && entry.WorkItem is RestoreOperationWorkItem restoreWorkItem)
            {
                RestoreExecutionResult execution;
                try
                {
                    var source = restoreSourceResolver.ResolveStoredSource(restoreWorkItem.Source, entry.Principal).ToDescriptor();
                    execution = await restoreService.ExecuteAsync(
                        restoreWorkItem.Settings,
                        source,
                        restoreWorkItem.Mode,
                        restoreWorkItem.ExpectedFingerprint,
                        operationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    execution = new RestoreExecutionResult(
                        PosAdminTool.Domain.Models.OperationResult.Running("restore_database"),
                        "restore.failed");
                    execution.Operation.AddError("Restore failed while applying the server-owned plan.");
                    execution.Operation.Finalize(PosAdminTool.Domain.Enums.OperationStatus.Failed);
                }

                foreach (var error in execution.Operation.Errors)
                {
                    entry.Report(execution.Operation.Status == PosAdminTool.Domain.Enums.OperationStatus.Cancelled ? 90 : 80, "warning", error);
                }

                var restoreOutcome = MapRestoreOutcome(execution);
                entry.Complete(
                    restoreOutcome.State,
                    restoreOutcome.ErrorCode,
                    // RestoreService has already finalized the destructive truth. A cancellation
                    // signal that arrives after ExecuteAsync returns must not rewrite it.
                    preserveOutcomeOnCancellation: true);
                await WriteAuditAsync().ConfigureAwait(false);
                registry.Publish(entry);
                return;
            }

            if (entry.Type is "cleanup" or "branch-reset"
                && entry.WorkItem is MaintenanceOperationWorkItem maintenanceWorkItem)
            {
                MaintenanceExecutionResult execution;
                try
                {
                    var settings = await configuration.LoadAsync(operationToken).ConfigureAwait(false);
                    execution = maintenanceWorkItem.Mode switch
                    {
                        MaintenanceMode.Cleanup => await maintenanceService.ExecuteCleanupAsync(
                            settings,
                            maintenanceWorkItem.ExpectedFingerprint,
                            new Progress<string>(message =>
                            {
                                entry.Report(60, "maintenance", message);
                                registry.Publish(entry);
                            }),
                            operationToken).ConfigureAwait(false),
                        MaintenanceMode.BranchReset => await maintenanceService.ExecuteBranchResetAsync(
                            settings,
                            maintenanceWorkItem.ExpectedFingerprint,
                            new Progress<string>(message =>
                            {
                                entry.Report(60, "maintenance", message);
                                registry.Publish(entry);
                            }),
                            operationToken).ConfigureAwait(false),
                        _ => FailedMaintenanceExecution(MaintenanceFailureCodes.InvalidConfiguration),
                    };
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    execution = FailedMaintenanceExecution(MaintenanceFailureCodes.OperationFailed);
                }

                entry.SetMaintenanceOutcome(MapMaintenanceEvidence(execution.Evidence));
                foreach (var warning in execution.Evidence.Warnings)
                {
                    entry.Report(80, "warning", warning);
                }

                var maintenanceOutcome = MapMaintenanceOutcome(execution);
                entry.Complete(
                    maintenanceOutcome.State,
                    maintenanceOutcome.ErrorCode,
                    preserveOutcomeOnCancellation: true);
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

    internal static (OperationState State, string? ErrorCode) MapRestoreOutcome(
        RestoreExecutionResult execution)
    {
        var state = execution.Operation.Status switch
        {
            OperationStatus.Success => OperationState.Succeeded,
            OperationStatus.PartialSuccess => OperationState.PartiallySucceeded,
            OperationStatus.Cancelled => OperationState.Cancelled,
            _ => OperationState.Failed,
        };

        var errorCode = state switch
        {
            OperationState.PartiallySucceeded => execution.FailureCode ?? RestoreFailureCodes.PartialFailure,
            OperationState.Failed => execution.FailureCode ?? "restore.failed",
            _ => null,
        };

        return (state, errorCode);
    }

    internal static (OperationState State, string? ErrorCode) MapMaintenanceOutcome(
        MaintenanceExecutionResult execution)
    {
        var state = execution.Operation.Status switch
        {
            OperationStatus.Success => OperationState.Succeeded,
            OperationStatus.PartialSuccess => OperationState.PartiallySucceeded,
            OperationStatus.Cancelled => OperationState.Cancelled,
            _ => OperationState.Failed,
        };

        var errorCode = state switch
        {
            OperationState.PartiallySucceeded => execution.FailureCode ?? MaintenanceFailureCodes.PartialFailure,
            OperationState.Failed => execution.FailureCode ?? MaintenanceFailureCodes.OperationFailed,
            _ => null,
        };

        return (state, errorCode);
    }

    private static MaintenanceOperationOutcomeDto MapMaintenanceEvidence(
        MaintenanceExecutionEvidence evidence) => new(
        evidence.DestructiveAttempted,
        evidence.RecoveryRequired,
        evidence.Items.Select(item => new MaintenanceItemOutcomeDto(
            item.TargetId,
            item.Kind,
            item.State switch
            {
                "already_absent" => MaintenanceItemState.AlreadyAbsent,
                "completed" => MaintenanceItemState.Completed,
                "rejected" => MaintenanceItemState.Rejected,
                "recovery_required" => MaintenanceItemState.RecoveryRequired,
                "failed" => MaintenanceItemState.Failed,
                _ => MaintenanceItemState.NotAttempted,
            },
            item.Attempted,
            item.Completed,
            item.ResidueUncertain,
            item.FailureCode,
            item.RecoveryGuidance)).ToList(),
        evidence.Warnings,
        evidence.RecoveryGuidance);

    private static MaintenanceExecutionResult FailedMaintenanceExecution(string errorCode)
    {
        var operation = PosAdminTool.Domain.Models.OperationResult.Running("maintenance");
        operation.AddError(errorCode);
        operation.Finalize(OperationStatus.Failed);
        return new(
            operation,
            new MaintenanceExecutionEvidence(false, false, [], [], []),
            errorCode);
    }

    private async Task<DownloaderWorkerResult> ExecuteDownloaderAsync(
        OperationRegistry.Entry entry,
        DownloaderOperationWorkItem workItem,
        DbDownloadService downloadService,
        ArtifactCatalog artifacts,
        IAgentSecretStore secrets,
        AgentConfigurationStoreOptions configurationOptions,
        TimeProvider timeProvider,
        CancellationToken operationToken,
        CancellationToken stoppingToken)
    {
        var branches = workItem.BranchCodes
            .Select(branch => new BranchBackupItem(branch))
            .ToList();

        string? password;
        try
        {
            password = await secrets.TryGetSecretAsync(AgentSecretKind.RdbPassword, operationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested || stoppingToken.IsCancellationRequested)
        {
            MarkCancelled(branches);
            return BuildDownloaderResult(
                branches,
                null,
                DownloaderTriggerState.NotAttempted,
                operationToken,
                stoppingToken);
        }

        // A configured username requires the matching service-owned secret. When neither is
        // configured, the SMB adapter may use the Agent's service identity and reports the
        // explicit NoCredentialRequired outcome; an orphaned password is never surfaced or sent.
        if (!string.IsNullOrWhiteSpace(workItem.Configuration.RdbUsername)
            && string.IsNullOrWhiteSpace(password))
        {
            foreach (var item in branches)
            {
                item.Status = BranchBackupStatus.Failed;
                item.FailureCode = DownloaderFailureCodes.CredentialMissing;
                item.ErrorMessage = "The RDB credential is not configured.";
            }

            return BuildDownloaderResult(
                branches,
                null,
                DownloaderTriggerState.NotAttempted,
                operationToken,
                stoppingToken);
        }

        var settings = new DbDownloaderSettings
        {
            ApiUrl = workItem.Configuration.ApiUrl,
            RdbServerIp = workItem.Configuration.RdbServerIp,
            RdbUsername = workItem.Configuration.RdbUsername,
            RdbPassword = password ?? string.Empty,
            BackupRootFolder = workItem.Configuration.BackupRootFolder,
            KnownBranchCodes = [.. workItem.Configuration.KnownBranchCodes],
            PollIntervalSeconds = workItem.Configuration.PollIntervalSeconds,
            TimeoutSeconds = workItem.Configuration.TimeoutSeconds,
            StableSizeObservationAttempts = workItem.Configuration.StableSizeObservationAttempts,
            StableSizeObservationIntervalSeconds = workItem.Configuration.StableSizeObservationIntervalSeconds
        };

        DownloaderExecutionResult execution;
        try
        {
            execution = await downloadService.RunWithOutcomeAsync(
                settings,
                workItem.BranchCodes,
                item => PublishDownloaderMirrorProgress(entry, branches, item),
                progress: null,
                operationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested || stoppingToken.IsCancellationRequested)
        {
            MarkCancelled(branches);
            return BuildDownloaderResult(
                branches,
                null,
                DownloaderTriggerState.NotAttempted,
                operationToken,
                stoppingToken);
        }
        catch
        {
            foreach (var item in branches)
            {
                item.Status = BranchBackupStatus.Failed;
                item.FailureCode = DownloaderFailureCodes.TriggerOutcomeUnknown;
                item.ErrorMessage = DownloaderOperatorGuidance.TriggerOutcomeUnknown;
            }

            return BuildDownloaderResult(
                branches,
                null,
                DownloaderTriggerState.OutcomeUnknown,
                operationToken,
                stoppingToken,
                DownloaderFailureCodes.TriggerOutcomeUnknown);
        }

        if (execution.TriggerState != DownloaderTriggerState.Accepted)
        {
            return BuildDownloaderResult(
                execution.Job.Items,
                execution.Job.Serial,
                execution.TriggerState,
                operationToken,
                stoppingToken,
                execution.FailureCode ?? (execution.TriggerState == DownloaderTriggerState.OutcomeUnknown
                    ? DownloaderFailureCodes.TriggerOutcomeUnknown
                    : DownloaderFailureCodes.TriggerFailed));
        }

        var job = execution.Job;
        PublishDownloaderProgress(entry, job, null, DownloaderTriggerState.Accepted);
        var stagingRoot = Path.Combine(configurationOptions.RootDirectory, "artifacts", "downloader", entry.Id);
        try
        {
            Directory.CreateDirectory(stagingRoot);
        }
        catch
        {
            foreach (var item in job.Items.Where(item => item.Status == BranchBackupStatus.Ready))
            {
                item.Status = BranchBackupStatus.Failed;
                item.FailureCode = DownloaderFailureCodes.InvalidConfiguration;
                item.ErrorMessage = "The Agent staging area is unavailable.";
            }

            return BuildDownloaderResult(
                job.Items,
                job.Serial,
                DownloaderTriggerState.Accepted,
                operationToken,
                stoppingToken,
                execution.FailureCode ?? DownloaderFailureCodes.InvalidConfiguration);
        }

        foreach (var item in job.Items.Where(item => item.Status == BranchBackupStatus.Ready).ToList())
        {
            if (operationToken.IsCancellationRequested || stoppingToken.IsCancellationRequested)
            {
                MarkCancelled(job.Items.Where(candidate => !IsTerminal(candidate)));
                break;
            }

            var branchFolder = Path.Combine(stagingRoot, item.BranchCode);
            using var downloadTimeout = CancellationTokenSource.CreateLinkedTokenSource(operationToken);
            using var downloadTimeoutTimer = timeProvider.CreateTimer(
                static state => ((CancellationTokenSource)state!).Cancel(),
                downloadTimeout,
                TimeSpan.FromSeconds(settings.TimeoutSeconds),
                Timeout.InfiniteTimeSpan);
            try
            {
                Directory.CreateDirectory(branchFolder);
                var progress = new Progress<double>(value =>
                {
                    entry.Report(
                        CalculateOverallProgress(job.Items, item, value),
                        "downloader",
                        $"{item.BranchCode}: downloading archive.");
                    entry.SetDownloaderOutcome(BuildOutcome(job.Items, job.Serial, DownloaderTriggerState.Accepted));
                    registry.Publish(entry);
                });
                await downloadService.DownloadAsync(
                    settings,
                    item,
                    branchFolder,
                    progress,
                    downloadTimeout.Token,
                    changed => PublishDownloaderProgress(entry, job, changed, DownloaderTriggerState.Accepted)).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(item.LocalDownloadPath) || !File.Exists(item.LocalDownloadPath))
                {
                    DeleteUnpublishedArchive(item);
                    item.Status = BranchBackupStatus.Failed;
                    item.FailureCode = DownloaderFailureCodes.ArtifactPublicationFailed;
                    item.ErrorMessage = "The downloaded archive was not available for publication.";
                    continue;
                }

                var metadata = artifacts.Register(
                    entry.Principal,
                    Path.GetFileName(item.LocalDownloadPath),
                    item.LocalDownloadPath,
                    new FileInfo(item.LocalDownloadPath).Length,
                    Convert.ToHexString(await ComputeSha256Async(item.LocalDownloadPath, downloadTimeout.Token).ConfigureAwait(false)),
                    timeProvider.GetUtcNow());
                item.ArtifactId = metadata.ArtifactId;
                PublishDownloaderProgress(entry, job, item, DownloaderTriggerState.Accepted);
            }
            catch (OperationCanceledException) when (downloadTimeout.IsCancellationRequested
                && !operationToken.IsCancellationRequested
                && !stoppingToken.IsCancellationRequested)
            {
                DeleteUnpublishedArchive(item);
                item.Status = BranchBackupStatus.TimedOut;
                item.FailureCode = DownloaderFailureCodes.Timeout;
                item.ErrorMessage = "The branch download timed out.";
                PublishDownloaderProgress(entry, job, item, DownloaderTriggerState.Accepted);
            }
            catch (OperationCanceledException) when (operationToken.IsCancellationRequested || stoppingToken.IsCancellationRequested)
            {
                DeleteUnpublishedArchive(item);
                item.Status = BranchBackupStatus.Cancelled;
                item.FailureCode = DownloaderFailureCodes.DownloadCancelled;
                item.ErrorMessage = "The branch download was cancelled.";
                MarkCancelled(job.Items.Where(candidate => !IsTerminal(candidate)));
                break;
            }
            catch (ArtifactCatalogCapacityException)
            {
                DeleteUnpublishedArchive(item);
                item.Status = BranchBackupStatus.Failed;
                item.FailureCode = DownloaderFailureCodes.ArtifactCatalogFull;
                item.ErrorMessage = "The Agent artifact retention limit was reached.";
                PublishDownloaderProgress(entry, job, item, DownloaderTriggerState.Accepted);
            }
            catch
            {
                DeleteUnpublishedArchive(item);
                item.Status = BranchBackupStatus.Failed;
                item.FailureCode = item.FailureCode ?? DownloaderFailureCodes.ArtifactPublicationFailed;
                item.ErrorMessage = "The branch archive could not be published.";
                PublishDownloaderProgress(entry, job, item, DownloaderTriggerState.Accepted);
            }
        }

        return BuildDownloaderResult(
            job.Items,
            job.Serial,
            DownloaderTriggerState.Accepted,
            operationToken,
            stoppingToken,
            execution.FailureCode);
    }

    private void PublishDownloaderProgress(
        OperationRegistry.Entry entry,
        BackupJob? job,
        BranchBackupItem? changed,
        DownloaderTriggerState triggerState)
    {
        if (job is null)
        {
            return;
        }

        var outcome = BuildOutcome(job.Items, job.Serial, triggerState);
        entry.SetDownloaderOutcome(outcome);
        if (changed is not null)
        {
            entry.Report(
                CalculateOverallProgress(job.Items, changed),
                "downloader",
                $"{changed.BranchCode}: {ToSafeState(changed.Status)}.");
        }

        registry.Publish(entry);
    }

    private void PublishDownloaderMirrorProgress(
        OperationRegistry.Entry entry,
        IReadOnlyList<BranchBackupItem> mirror,
        BranchBackupItem changed)
    {
        var current = mirror.FirstOrDefault(item =>
            string.Equals(item.BranchCode, changed.BranchCode, StringComparison.OrdinalIgnoreCase));
        if (current is null) return;

        current.Status = changed.Status;
        current.FailureCode = changed.FailureCode;
        current.ArtifactId = changed.ArtifactId;
        current.LastObservedSizeBytes = changed.LastObservedSizeBytes;
        entry.SetDownloaderOutcome(BuildOutcome(mirror, null, DownloaderTriggerState.Accepted));
        entry.Report(
            CalculateOverallProgress(mirror, current),
            "downloader",
            $"{current.BranchCode}: {ToSafeState(current.Status)}.");
        registry.Publish(entry);
    }

    private static DownloaderWorkerResult BuildDownloaderResult(
        IReadOnlyList<BranchBackupItem> items,
        string? serial,
        DownloaderTriggerState triggerState,
        CancellationToken operationToken,
        CancellationToken stoppingToken,
        string? failureCode = null)
    {
        var completed = items.Count(item => item.Status == BranchBackupStatus.Downloaded);
        var cancelled = items.Count(item => item.Status == BranchBackupStatus.Cancelled);
        var failed = items.Count(item => item.Status == BranchBackupStatus.Failed);
        var timedOut = items.Count(item => item.Status == BranchBackupStatus.TimedOut);
        var allTerminal = items.All(IsTerminal);
        var cancellationRequested = operationToken.IsCancellationRequested || stoppingToken.IsCancellationRequested;

        OperationState state;
        string? errorCode;
        if (triggerState == DownloaderTriggerState.OutcomeUnknown)
        {
            state = OperationState.Failed;
            errorCode = failureCode ?? DownloaderFailureCodes.TriggerOutcomeUnknown;
        }
        else if (completed == items.Count && items.Count > 0)
        {
            state = OperationState.Succeeded;
            errorCode = null;
        }
        else if (completed > 0)
        {
            state = OperationState.PartiallySucceeded;
            errorCode = cancellationRequested ? DownloaderFailureCodes.CancelledAfterPartial : DownloaderFailureCodes.PartialFailure;
        }
        else if (cancellationRequested || cancelled == items.Count)
        {
            state = OperationState.Cancelled;
            errorCode = null;
        }
        else if (timedOut > 0 && failed == 0)
        {
            state = OperationState.Failed;
            errorCode = failureCode ?? DownloaderFailureCodes.Timeout;
        }
        else
        {
            state = OperationState.Failed;
            errorCode = allTerminal && failed + timedOut == items.Count
                ? failureCode ?? DownloaderFailureCodes.PartialFailure
                : failureCode ?? DownloaderFailureCodes.TriggerFailed;
        }

        return new DownloaderWorkerResult(BuildOutcome(items, serial, triggerState), state, errorCode, items.Where(item => item.ArtifactId is not null).Select(item => item.ArtifactId!).ToList());
    }

    private static DownloaderOperationOutcomeDto BuildOutcome(
        IReadOnlyList<BranchBackupItem> items,
        string? serial,
        DownloaderTriggerState triggerState) =>
        new(
            items.Select(item => new DownloaderBranchOutcomeDto(
                item.BranchCode,
                MapBranchState(item.Status),
                BranchProgress(item.Status),
                item.FailureCode,
                item.ArtifactId)).ToList(),
            serial,
            MapTriggerState(triggerState),
            triggerState == DownloaderTriggerState.OutcomeUnknown
                ? DownloaderOperatorGuidance.TriggerOutcomeUnknown
                : null);

    private static DownloaderTriggerStateDto MapTriggerState(DownloaderTriggerState state) => state switch
    {
        DownloaderTriggerState.NotAttempted => DownloaderTriggerStateDto.NotAttempted,
        DownloaderTriggerState.Failed => DownloaderTriggerStateDto.Failed,
        DownloaderTriggerState.Accepted => DownloaderTriggerStateDto.Accepted,
        DownloaderTriggerState.OutcomeUnknown => DownloaderTriggerStateDto.OutcomeUnknown,
        _ => DownloaderTriggerStateDto.OutcomeUnknown,
    };

    private static DownloaderBranchState MapBranchState(BranchBackupStatus status) => status switch
    {
        BranchBackupStatus.Triggered => DownloaderBranchState.Triggered,
        BranchBackupStatus.Waiting => DownloaderBranchState.Waiting,
        BranchBackupStatus.ZipDetected => DownloaderBranchState.Detected,
        BranchBackupStatus.Validating => DownloaderBranchState.Validating,
        BranchBackupStatus.Ready => DownloaderBranchState.Ready,
        BranchBackupStatus.Downloading => DownloaderBranchState.Downloading,
        BranchBackupStatus.Downloaded => DownloaderBranchState.Completed,
        BranchBackupStatus.TimedOut => DownloaderBranchState.TimedOut,
        BranchBackupStatus.Cancelled => DownloaderBranchState.Cancelled,
        BranchBackupStatus.Failed => DownloaderBranchState.Failed,
        _ => DownloaderBranchState.Pending
    };

    private static int BranchProgress(BranchBackupStatus status) => status switch
    {
        BranchBackupStatus.Triggered => 10,
        BranchBackupStatus.Waiting => 20,
        BranchBackupStatus.ZipDetected => 35,
        BranchBackupStatus.Validating => 45,
        BranchBackupStatus.Ready => 55,
        BranchBackupStatus.Downloading => 65,
        BranchBackupStatus.Downloaded or BranchBackupStatus.TimedOut or BranchBackupStatus.Cancelled or BranchBackupStatus.Failed => 100,
        _ => 0
    };

    private static int CalculateOverallProgress(IReadOnlyList<BranchBackupItem> items, BranchBackupItem changed, double? downloadProgress = null)
    {
        if (items.Count == 0) return 0;
        var total = items.Sum(item =>
        {
            if (ReferenceEquals(item, changed) && downloadProgress is { } value && item.Status == BranchBackupStatus.Downloading)
            {
                return 60 + (int)Math.Round(Math.Clamp(value, 0, 1) * 40);
            }

            return BranchProgress(item.Status);
        });
        return Math.Clamp(total / items.Count, 0, 100);
    }

    private static string ToSafeState(BranchBackupStatus status) => status switch
    {
        BranchBackupStatus.Downloaded => "completed",
        BranchBackupStatus.TimedOut => "timed out",
        BranchBackupStatus.Cancelled => "cancelled",
        BranchBackupStatus.Failed => "failed",
        _ => status.ToString().ToLowerInvariant()
    };

    private static void MarkCancelled(IEnumerable<BranchBackupItem> items)
    {
        foreach (var item in items)
        {
            if (IsTerminal(item)) continue;
            item.Status = BranchBackupStatus.Cancelled;
            item.FailureCode = DownloaderFailureCodes.DownloadCancelled;
            item.ErrorMessage = "The downloader operation was cancelled.";
        }
    }

    private static bool IsTerminal(BranchBackupItem item) => item.Status is
        BranchBackupStatus.Downloaded
        or BranchBackupStatus.Failed
        or BranchBackupStatus.TimedOut
        or BranchBackupStatus.Cancelled;

    private static void DeleteUnpublishedArchive(BranchBackupItem item)
    {
        if (item.ArtifactId is not null || string.IsNullOrWhiteSpace(item.LocalDownloadPath)) return;

        try
        {
            if (File.Exists(item.LocalDownloadPath)) File.Delete(item.LocalDownloadPath);
        }
        catch
        {
            // An unpublished cleanup failure never enters the Agent contract. A later service-owned
            // staging sweep can remove the orphan without deleting a published artifact.
        }
    }

    private static async Task<byte[]> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private sealed record DownloaderWorkerResult(
        DownloaderOperationOutcomeDto Outcome,
        OperationState State,
        string? ErrorCode,
        IReadOnlyList<string> ArtifactIds);
}

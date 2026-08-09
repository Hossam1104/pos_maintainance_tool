using PosAdminTool.Agent;
using PosAdminTool.Agent.Audit;
using PosAdminTool.Agent.Operations;
using PosAdminTool.Agent.IntegrationTests.TestSupport;
using PosAdminTool.Application.Restore;
using PosAdminTool.Contracts.V1.Operations;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Models;
using PosAdminTool.Infrastructure.Configuration;

namespace PosAdminTool.Agent.IntegrationTests;

public sealed class OperationRegistryTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SubmittedOperation_IsOpaqueQueuedAndCanBeCancelledBeforeItRuns()
    {
        var registry = new OperationRegistry();

        Assert.True(registry.TrySubmit("diagnostic", "B001", "TEST\\admin", "correlation", null, out var submitted, out _));
        Assert.NotNull(submitted);
        Assert.Matches("^[a-f0-9]{32}$", submitted!.OperationId);
        Assert.Equal(OperationState.Queued, submitted.State);
        Assert.True(registry.Cancel(submitted.OperationId, out var cancelled));
        Assert.Equal(OperationState.Cancelled, cancelled!.State);
    }

    [Fact]
    public void Registry_RejectsWhenBoundedQueueIsFull()
    {
        var registry = new OperationRegistry();
        for (var index = 0; index < 32; index++)
        {
            Assert.True(registry.TrySubmit("diagnostic", "B001", "TEST\\admin", "c", null, out _, out _));
        }

        Assert.False(registry.TrySubmit("diagnostic", "B001", "TEST\\admin", "c", null, out _, out _));
    }

    [Fact]
    public async Task CompletedEntries_ExpireAtTheInjectableClockBoundary_AndReleaseIdempotency()
    {
        var clock = new ManualTimeProvider(Start);
        var policy = new RuntimeRetentionPolicy
        {
            MaxCompletedOperations = 4,
            CompletedOperationLifetime = TimeSpan.FromHours(1),
        };
        var registry = new OperationRegistry(policy, clock);

        Assert.True(registry.TrySubmit("diagnostic", "B001", "TEST\\admin", "c", "retry-key", out var submitted, out _));
        var entry = await ReadOneAsync(registry);
        entry.TryStart();
        entry.Complete(OperationState.Succeeded);
        registry.Publish(entry);

        Assert.True(registry.TryGet(submitted!.OperationId, out _));
        Assert.True(registry.TryGetIdempotent("TEST\\admin", "retry-key", out var retained));
        Assert.Equal(submitted.OperationId, retained!.OperationId);

        clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(1, registry.Prune());
        Assert.False(registry.TryGet(submitted.OperationId, out _));
        Assert.False(registry.TryGetIdempotent("TEST\\admin", "retry-key", out _));
        Assert.True(registry.TrySubmit("diagnostic", "B001", "TEST\\admin", "c", "retry-key", out var replacement, out var duplicate));
        Assert.False(duplicate);
        Assert.NotEqual(submitted.OperationId, replacement!.OperationId);
    }

    [Fact]
    public async Task CompletedCountRetention_NeverEvictsQueuedOrRunningEntries()
    {
        var clock = new ManualTimeProvider(Start);
        var policy = new RuntimeRetentionPolicy
        {
            MaxCompletedOperations = 1,
            CompletedOperationLifetime = TimeSpan.FromHours(1),
        };
        var registry = new OperationRegistry(policy, clock);

        Assert.True(registry.TrySubmit("diagnostic", "B001", "TEST\\admin", "c", null, out var first, out _));
        var firstEntry = await ReadOneAsync(registry);
        firstEntry.TryStart();
        firstEntry.Complete(OperationState.Succeeded);
        registry.Publish(firstEntry);

        Assert.True(registry.TrySubmit("diagnostic", "B001", "TEST\\admin", "c", null, out var queued, out _));
        clock.Advance(TimeSpan.FromHours(1));
        registry.Prune();
        Assert.False(registry.TryGet(first!.OperationId, out _));
        Assert.True(registry.TryGet(queued!.OperationId, out var queuedDetail));
        Assert.Equal(OperationState.Queued, queuedDetail!.State);

        var queuedEntry = await ReadOneAsync(registry);
        Assert.True(queuedEntry.TryStart());
        registry.Publish(queuedEntry);
        clock.Advance(TimeSpan.FromHours(1));
        registry.Prune();

        Assert.True(registry.TryGet(queued.OperationId, out var runningDetail));
        Assert.Equal(OperationState.Running, runningDetail!.State);
    }

    [Fact]
    public async Task ActiveIdempotency_ReturnsTheExistingOperationWithoutDuplicatingIt()
    {
        var registry = new OperationRegistry(new ManualTimeProvider(Start));
        Assert.True(registry.TrySubmit("diagnostic", "B001", "TEST\\admin", "c", "same-key", out var first, out var firstDuplicate));
        Assert.False(firstDuplicate);

        Assert.True(registry.TrySubmit("diagnostic", "B001", "TEST\\admin", "c", "same-key", out var second, out var secondDuplicate));

        Assert.True(secondDuplicate);
        Assert.Equal(first!.OperationId, second!.OperationId);
        Assert.Single(registry.List());
    }

    [Fact]
    public async Task Events_RemainBounded_WhileQueuedRunningTerminalAndSafeEvidenceSurvive()
    {
        var policy = new RuntimeRetentionPolicy { MaxEventsPerOperation = 8 };
        var registry = new OperationRegistry(policy, new ManualTimeProvider(Start));
        Assert.True(registry.TrySubmit("diagnostic", "B001", "TEST\\admin", "c", null, out var submitted, out _));
        var entry = await ReadOneAsync(registry);
        Assert.True(entry.TryStart());

        for (var index = 0; index < 100; index++)
        {
            entry.Report(index, "progress", $"progress {index}");
        }

        entry.Report(
            90,
            "warning",
            "password=super-secret C:\\private\\backup.zip\r\nSystem.InvalidOperationException: leaked exception");
        entry.Complete(OperationState.Failed, "backup.failed");

        var detail = entry.ToDto();
        Assert.Equal(submitted!.OperationId, detail.OperationId);
        Assert.InRange(detail.Events.Count, 1, 8);
        Assert.Contains(detail.Events, item => item.Stage == "queued");
        Assert.Contains(detail.Events, item => item.Stage == "running");
        Assert.Contains(detail.Events, item => item.Stage == "failed");
        Assert.Equal("backup.failed", detail.ErrorCode);
        Assert.All(detail.Events, item =>
        {
            Assert.DoesNotContain('\r', item.Message);
            Assert.DoesNotContain('\n', item.Message);
            Assert.DoesNotContain("super-secret", item.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("C:\\private", item.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("InvalidOperationException", item.Message, StringComparison.Ordinal);
            Assert.InRange(item.Message.Length, 0, 512);
        });
    }

    [Fact]
    public async Task Activity_IsBoundedByTheExplicitActivityPolicy()
    {
        var clock = new ManualTimeProvider(Start);
        var policy = new RuntimeRetentionPolicy
        {
            MaxCompletedOperations = 10,
            MaxActivityEntries = 2,
        };
        var registry = new OperationRegistry(policy, clock);

        for (var index = 0; index < 5; index++)
        {
            Assert.True(registry.TrySubmit("diagnostic", "B001", "TEST\\admin", "c", null, out _, out _));
            var entry = await ReadOneAsync(registry);
            entry.TryStart();
            entry.Complete(OperationState.Succeeded);
            registry.Publish(entry);
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.Equal(2, registry.ListActivity().Count);
    }

    [Fact]
    public async Task ShutdownCancellation_MarksQueuedAndRunningEntriesTerminalAndClearsWorkItems()
    {
        var registry = new OperationRegistry(new ManualTimeProvider(Start));
        Assert.True(registry.TrySubmit("diagnostic", "B001", "TEST\\admin", "c", null, "queued-work", null, out var queued, out _));
        Assert.True(registry.TrySubmit("diagnostic", "B001", "TEST\\admin", "c", null, "running-work", null, out var running, out _));
        var runningEntry = await ReadOneAsync(registry);
        Assert.True(runningEntry.TryStart());

        var cancelled = registry.CancelActive();
        var queuedEntry = await ReadOneAsync(registry);

        Assert.Equal(2, cancelled.Count);
        Assert.All(cancelled, entry => Assert.Equal(OperationState.Cancelled, entry.State));
        Assert.Null(queuedEntry.WorkItem);
        Assert.Null(runningEntry.WorkItem);
        Assert.True(registry.TryGet(queued!.OperationId, out var queuedDetail));
        Assert.Equal(OperationState.Cancelled, queuedDetail!.State);
        Assert.True(registry.TryGet(running!.OperationId, out var runningDetail));
        Assert.Equal(OperationState.Cancelled, runningDetail!.State);
    }

    [Fact]
    public void InvalidTransition_IsRejected()
    {
        var entry = new OperationRegistry.Entry("diagnostic", "B001", "TEST\\admin", "c");
        Assert.Throws<InvalidOperationException>(() => entry.Complete(OperationState.Succeeded));
    }

    [Fact]
    public void RestoreEntriesAreDestructiveAuditedAndCarryAllConflictingResourceLocks()
    {
        var entry = new OperationRegistry.Entry("restore", "B001", "TEST\\admin", "c");

        Assert.True(entry.IsDestructive);
        Assert.True(entry.NeedsAudit);
        Assert.Equal(["sql", "services", "filesystem-cleanup"], entry.Locks);
    }

    [Fact]
    public void RestoreSuccess_IsAuthoritativeWhenCancellationArrivesBeforeWorkerMapping()
    {
        var execution = CreateRestoreExecution(OperationStatus.Success);
        var entry = StartRestoreEntry();
        entry.Cancel();

        Assert.True(entry.Token.IsCancellationRequested);
        var mapped = OperationWorker.MapRestoreOutcome(execution);

        Assert.Equal(OperationState.Succeeded, mapped.State);
        Assert.Null(mapped.ErrorCode);

        entry.Complete(mapped.State, mapped.ErrorCode, preserveOutcomeOnCancellation: true);

        var detail = entry.ToDto();
        Assert.Equal(OperationState.Succeeded, detail.State);
        Assert.Null(detail.ErrorCode);
    }

    [Fact]
    public void RestorePartialResult_PreservesFailureCodeWhenLateCancellationIsRequested()
    {
        var execution = CreateRestoreExecution(
            OperationStatus.PartialSuccess,
            RestoreFailureCodes.ConfigRollbackFailed);
        var entry = StartRestoreEntry();
        entry.Cancel();

        var mapped = OperationWorker.MapRestoreOutcome(execution);

        Assert.Equal(OperationState.PartiallySucceeded, mapped.State);
        Assert.Equal(RestoreFailureCodes.ConfigRollbackFailed, mapped.ErrorCode);

        entry.Complete(mapped.State, mapped.ErrorCode, preserveOutcomeOnCancellation: true);

        var detail = entry.ToDto();
        Assert.Equal(OperationState.PartiallySucceeded, detail.State);
        Assert.Equal(RestoreFailureCodes.ConfigRollbackFailed, detail.ErrorCode);
    }

    [Fact]
    public void RestoreFailure_RemainsFailureWhenCancellationArrivesAfterExecutionResult()
    {
        var execution = CreateRestoreExecution(OperationStatus.Failed, "restore.failed");
        var entry = StartRestoreEntry();
        var mapped = OperationWorker.MapRestoreOutcome(execution);

        entry.Cancel();

        Assert.Equal(OperationState.Failed, mapped.State);
        Assert.Equal("restore.failed", mapped.ErrorCode);

        entry.Complete(mapped.State, mapped.ErrorCode, preserveOutcomeOnCancellation: true);

        var detail = entry.ToDto();
        Assert.Equal(OperationState.Failed, detail.State);
        Assert.Equal("restore.failed", detail.ErrorCode);
    }

    [Fact]
    public void RestoreCancellation_IsMappedFromTheFinalizedServiceResult()
    {
        var execution = CreateRestoreExecution(OperationStatus.Cancelled);
        var mapped = OperationWorker.MapRestoreOutcome(execution);

        Assert.Equal(OperationState.Cancelled, mapped.State);
        Assert.Null(mapped.ErrorCode);

        var entry = StartRestoreEntry();
        entry.Complete(mapped.State, mapped.ErrorCode, preserveOutcomeOnCancellation: true);

        var detail = entry.ToDto();
        Assert.Equal(OperationState.Cancelled, detail.State);
        Assert.Null(detail.ErrorCode);
    }

    [Fact]
    public void RestoreEntryCompletion_PreservesEveryFinalizedNonCancelledOutcomeAfterCancellation()
    {
        var outcomes = new (OperationState State, string? ErrorCode)[]
        {
            (OperationState.Succeeded, null),
            (OperationState.PartiallySucceeded, RestoreFailureCodes.PartialFailure),
            (OperationState.Failed, "restore.failed"),
        };

        foreach (var outcome in outcomes)
        {
            var entry = StartRestoreEntry();
            entry.Cancel();

            entry.Complete(
                outcome.State,
                outcome.ErrorCode,
                preserveOutcomeOnCancellation: true);

            var detail = entry.ToDto();
            Assert.Equal(outcome.State, detail.State);
            Assert.Equal(outcome.ErrorCode, detail.ErrorCode);
        }
    }

    [Fact]
    public async Task PartialRestoreAuditCarriesOnlyTheStableFailureCode()
    {
        var root = Directory.CreateTempSubdirectory("pos-restore-audit-tests-");
        try
        {
            var entry = new OperationRegistry.Entry("restore", "B001", "TEST\\admin", "correlation", destinationReference: "C:\\private\\restore.zip");
            Assert.True(entry.TryStart());
            entry.Complete(OperationState.PartiallySucceeded, RestoreFailureCodes.ConfigRollbackFailed);

            await new OperationAuditWriter(new AgentConfigurationStoreOptions { RootDirectory = root.FullName })
                .AppendAsync(entry, CancellationToken.None);

            var audit = await File.ReadAllTextAsync(Path.Combine(root.FullName, "audit", "operations.jsonl"));
            Assert.Contains("\"state\":\"PartiallySucceeded\"", audit, StringComparison.Ordinal);
            Assert.Contains($"\"errorCode\":\"{RestoreFailureCodes.ConfigRollbackFailed}\"", audit, StringComparison.Ordinal);
            Assert.DoesNotContain("C:\\private", audit, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", audit, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root.FullName)) Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Fact]
    public async Task RestoreAuditCarriesSanitizedModeAndLogicalTargetForEveryRestoreMode()
    {
        var root = Directory.CreateTempSubdirectory("pos-restore-intent-audit-tests-");
        try
        {
            var writer = new OperationAuditWriter(new AgentConfigurationStoreOptions { RootDirectory = root.FullName });
            foreach (var mode in new[] { "full", "database-only", "config-only" })
            {
                var entry = new OperationRegistry.Entry(
                    "restore",
                    "B001",
                    "TEST\\admin",
                    "correlation",
                    destinationReference: "C:\\private\\restore.zip",
                    operationMode: mode,
                    operationTarget: "RmsBranchSrv");
                Assert.True(entry.TryStart());
                entry.Complete(OperationState.PartiallySucceeded, RestoreFailureCodes.DatabaseRestoreInterrupted);
                await writer.AppendAsync(entry, CancellationToken.None);
            }

            var audit = await File.ReadAllTextAsync(Path.Combine(root.FullName, "audit", "operations.jsonl"));
            Assert.Contains("\"operationMode\":\"full\"", audit, StringComparison.Ordinal);
            Assert.Contains("\"operationMode\":\"database-only\"", audit, StringComparison.Ordinal);
            Assert.Contains("\"operationMode\":\"config-only\"", audit, StringComparison.Ordinal);
            Assert.Equal(3, audit.Split("\"operationTarget\":\"RmsBranchSrv\"", StringSplitOptions.None).Length - 1);
            Assert.Equal(3, audit.Split("\"state\":\"PartiallySucceeded\"", StringSplitOptions.None).Length - 1);
            Assert.Equal(3, audit.Split($"\"errorCode\":\"{RestoreFailureCodes.DatabaseRestoreInterrupted}\"", StringSplitOptions.None).Length - 1);
            Assert.DoesNotContain("C:\\private", audit, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", audit, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", audit, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("connectionString", audit, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root.FullName)) Directory.Delete(root.FullName, recursive: true);
        }
    }

    private static async Task<OperationRegistry.Entry> ReadOneAsync(OperationRegistry registry)
    {
        await foreach (var entry in registry.ReadAllAsync(CancellationToken.None))
        {
            return entry;
        }

        throw new InvalidOperationException("The operation queue completed unexpectedly.");
    }

    private static RestoreExecutionResult CreateRestoreExecution(
        OperationStatus status,
        string? failureCode = null)
    {
        var result = OperationResult.Running("restore_database");
        result.Finalize(status);
        return new RestoreExecutionResult(result, failureCode);
    }

    private static OperationRegistry.Entry StartRestoreEntry()
    {
        var entry = new OperationRegistry.Entry("restore", "B001", "TEST\\admin", "c");
        Assert.True(entry.TryStart());
        return entry;
    }
}

using PosAdminTool.Agent.Operations;
using PosAdminTool.Application.Maintenance;
using PosAdminTool.Contracts.V1.Maintenance;
using PosAdminTool.Contracts.V1.Operations;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Agent.IntegrationTests;

public sealed class MaintenanceOperationTests
{
    [Fact]
    public void CleanupAndBranchResetAreDestructiveAuditedAndUseSpecificResourceLocks()
    {
        var cleanup = new OperationRegistry.Entry("cleanup", "NORTH_EU_01", "TESTDOMAIN\\admin", "cleanup-correlation");
        var reset = new OperationRegistry.Entry("branch-reset", "NORTH_EU_01", "TESTDOMAIN\\admin", "reset-correlation");

        Assert.True(cleanup.IsDestructive);
        Assert.True(cleanup.NeedsAudit);
        Assert.Equal(["services", "filesystem-cleanup"], cleanup.Locks);
        Assert.True(reset.IsDestructive);
        Assert.True(reset.NeedsAudit);
        Assert.Equal(["sql", "services"], reset.Locks);
    }

    [Fact]
    public async Task ConflictingMaintenanceLocksSerializeAndPrincipalIdempotencyReturnsTheOriginalEntry()
    {
        var locks = new ResourceLockSet();
        using var held = await locks.AcquireAsync(["services", "filesystem-cleanup"], CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var waiting = locks.AcquireAsync(["filesystem-cleanup", "services"], cancellation.Token);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await waiting);
        held.Dispose();

        using var reacquired = await locks.AcquireAsync(["services", "filesystem-cleanup"], CancellationToken.None);
        var registry = new OperationRegistry();
        Assert.True(registry.TrySubmit("cleanup", "NORTH_EU_01", "TESTDOMAIN\\admin", "c", "same-maintenance-key", out var first, out var firstDuplicate));
        Assert.False(firstDuplicate);
        Assert.True(registry.TrySubmit("cleanup", "NORTH_EU_01", "TESTDOMAIN\\admin", "c2", "same-maintenance-key", out var second, out var secondDuplicate));
        Assert.True(secondDuplicate);
        Assert.Equal(first!.OperationId, second!.OperationId);
    }

    [Fact]
    public void LateCancellationDoesNotRewriteAnAlreadyFinalizedMaintenanceResult()
    {
        var operation = OperationResult.Running("cleanup_files");
        operation.Finalize(OperationStatus.Success);
        var execution = new MaintenanceExecutionResult(
            operation,
            new MaintenanceExecutionEvidence(
                true,
                false,
                [new("cleanup-001", "file", "completed", true, true, false, null, null)],
                [],
                []));
        var entry = new OperationRegistry.Entry("cleanup", "NORTH_EU_01", "TESTDOMAIN\\admin", "c");
        Assert.True(entry.TryStart());
        entry.Cancel();

        var mapped = OperationWorker.MapMaintenanceOutcome(execution);
        entry.SetMaintenanceOutcome(new MaintenanceOperationOutcomeDto(
            execution.Evidence.DestructiveAttempted,
            execution.Evidence.RecoveryRequired,
            [new("cleanup-001", "file", MaintenanceItemState.Completed, true, true, false, null, null)],
            [],
            []));
        entry.Complete(mapped.State, mapped.ErrorCode, preserveOutcomeOnCancellation: true);

        var detail = entry.ToDto();
        Assert.Equal(OperationState.Succeeded, detail.State);
        Assert.NotNull(detail.MaintenanceOutcome);
        Assert.Equal(MaintenanceItemState.Completed, detail.MaintenanceOutcome!.Items.Single().State);
    }
}

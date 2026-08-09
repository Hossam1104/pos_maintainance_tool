using PosAdminTool.Agent.Operations;

namespace PosAdminTool.Agent.IntegrationTests;

public sealed class ResourceLockSetTests
{
    [Fact]
    public async Task CancellationWhileWaiting_ReleasesAnyPartiallyAcquiredLocks()
    {
        var locks = new ResourceLockSet();
        using var held = await locks.AcquireAsync(["sql"], CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var waiting = locks.AcquireAsync(["sql", "services"], cancellation.Token);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await waiting);
        held.Dispose();

        using var reacquired = await locks.AcquireAsync(["sql", "services"], CancellationToken.None);
    }

    [Fact]
    public async Task ReleasingTheSameLeaseTwice_DoesNotOverReleaseTheSemaphore()
    {
        var locks = new ResourceLockSet();
        var lease = await locks.AcquireAsync(["sql"], CancellationToken.None);
        lease.Dispose();
        lease.Dispose();

        using var reacquired = await locks.AcquireAsync(["sql"], CancellationToken.None);
    }

    [Fact]
    public async Task RestoreResourceScopeConflictsWithSqlServiceAndCleanupWork()
    {
        var locks = new ResourceLockSet();
        using var held = await locks.AcquireAsync(["sql", "services", "filesystem-cleanup"], CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var waiting = locks.AcquireAsync(["filesystem-cleanup", "services", "sql"], cancellation.Token);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await waiting);
        held.Dispose();
        using var reacquired = await locks.AcquireAsync(["sql", "services", "filesystem-cleanup"], CancellationToken.None);
    }
}

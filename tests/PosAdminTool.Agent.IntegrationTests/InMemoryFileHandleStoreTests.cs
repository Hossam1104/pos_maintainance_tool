using PosAdminTool.Agent.Files;
using PosAdminTool.Agent.IntegrationTests.TestSupport;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Files;

namespace PosAdminTool.Agent.IntegrationTests;

public class InMemoryFileHandleStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Redeem_ValidHandle_SucceedsAndReturnsOriginalTarget()
    {
        var store = new InMemoryFileHandleStore(new ManualTimeProvider(Start));
        var handle = store.Issue("DOMAIN\\alice", "root-1", "sub/path", FileHandlePurpose.RestoreSource);

        var result = store.Redeem(handle.HandleId, "DOMAIN\\alice", FileHandlePurpose.RestoreSource);

        Assert.True(result.Success);
        Assert.Equal("root-1", result.RootId);
        Assert.Equal("sub/path", result.RelativeSubPath);
    }

    [Fact]
    public void Redeem_UnknownHandle_Fails()
    {
        var store = new InMemoryFileHandleStore(new ManualTimeProvider(Start));

        var result = store.Redeem("does-not-exist", "DOMAIN\\alice", FileHandlePurpose.RestoreSource);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.HandleNotFound, result.FailureErrorCode);
    }

    [Fact]
    public void Redeem_ExpiredHandle_Fails()
    {
        var clock = new ManualTimeProvider(Start);
        var store = new InMemoryFileHandleStore(clock);
        var handle = store.Issue("DOMAIN\\alice", "root-1", "sub/path", FileHandlePurpose.RestoreSource);

        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        var result = store.Redeem(handle.HandleId, "DOMAIN\\alice", FileHandlePurpose.RestoreSource);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.HandleExpired, result.FailureErrorCode);
    }

    [Fact]
    public void Redeem_WrongPrincipal_FailsAndDoesNotConsumeTheHandle()
    {
        var store = new InMemoryFileHandleStore(new ManualTimeProvider(Start));
        var handle = store.Issue("DOMAIN\\alice", "root-1", "sub/path", FileHandlePurpose.RestoreSource);

        var attackerAttempt = store.Redeem(handle.HandleId, "DOMAIN\\mallory", FileHandlePurpose.RestoreSource);
        Assert.False(attackerAttempt.Success);
        Assert.Equal(ErrorCodes.HandleWrongPrincipal, attackerAttempt.FailureErrorCode);

        var legitimateAttempt = store.Redeem(handle.HandleId, "DOMAIN\\alice", FileHandlePurpose.RestoreSource);
        Assert.True(legitimateAttempt.Success);
    }

    [Fact]
    public void Redeem_WrongPurpose_Fails()
    {
        var store = new InMemoryFileHandleStore(new ManualTimeProvider(Start));
        var handle = store.Issue("DOMAIN\\alice", "root-1", "sub/path", FileHandlePurpose.RestoreSource);

        var result = store.Redeem(handle.HandleId, "DOMAIN\\alice", FileHandlePurpose.BackupDestination);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.HandleWrongPurpose, result.FailureErrorCode);
    }

    [Fact]
    public void Redeem_AlreadyUsedHandle_Fails()
    {
        var store = new InMemoryFileHandleStore(new ManualTimeProvider(Start));
        var handle = store.Issue("DOMAIN\\alice", "root-1", "sub/path", FileHandlePurpose.RestoreSource);

        var first = store.Redeem(handle.HandleId, "DOMAIN\\alice", FileHandlePurpose.RestoreSource);
        var second = store.Redeem(handle.HandleId, "DOMAIN\\alice", FileHandlePurpose.RestoreSource);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal(ErrorCodes.HandleAlreadyUsed, second.FailureErrorCode);
    }
}

using PosAdminTool.Agent.Operations;
using PosAdminTool.Contracts.V1.Operations;

namespace PosAdminTool.Agent.IntegrationTests;

public sealed class OperationRegistryTests
{
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
    public void InvalidTransition_IsRejected()
    {
        var entry = new OperationRegistry.Entry("diagnostic", "B001", "TEST\\admin", "c");
        Assert.Throws<InvalidOperationException>(() => entry.Complete(OperationState.Succeeded));
    }
}

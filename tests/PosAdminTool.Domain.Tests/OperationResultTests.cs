using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Domain.Tests;

public sealed class OperationResultTests
{
    [Fact]
    public void FinalizeSuccessMarksResultSuccessful()
    {
        var result = OperationResult.Running("backup_database");

        result.AddMessage("done");
        result.Finalize(OperationStatus.Success);

        Assert.True(result.Success);
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.NotNull(result.EndTime);
        Assert.Contains("done", result.Messages);
    }
}

using PosAdminTool.Domain.Enums;

namespace PosAdminTool.Domain.Models;

public sealed class BranchBackupItem(string branchCode)
{
    public string BranchCode { get; } = branchCode;

    public BranchBackupStatus Status { get; set; } = BranchBackupStatus.Pending;

    public string? RemoteZipPath { get; set; }

    public long LastObservedSizeBytes { get; set; } = -1;

    public string? LocalDownloadPath { get; set; }

    public string? ErrorMessage { get; set; }
}

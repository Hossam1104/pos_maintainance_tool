namespace PosAdminTool.Domain.Enums;

public enum BranchBackupStatus
{
    Pending,
    ZipDetected,
    Validating,
    Ready,
    Downloading,
    Downloaded,
    Failed,
    TimedOut
}

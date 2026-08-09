namespace PosAdminTool.Domain.Models;

public enum DownloaderTriggerState
{
    NotAttempted,
    Failed,
    Accepted,
}

/// <summary>
/// Application-owned downloader lifecycle truth. The trigger milestone remains available after
/// discovery, repository, download, or cancellation work fails later.
/// </summary>
public sealed record DownloaderExecutionResult(
    BackupJob Job,
    DownloaderTriggerState TriggerState,
    string? FailureCode = null)
{
    public bool TriggerAccepted => TriggerState == DownloaderTriggerState.Accepted;
}

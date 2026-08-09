namespace PosAdminTool.Contracts.V1.Downloader;

/// <summary>Safe browser-facing truth for the remote backup-trigger lifecycle.</summary>
public enum DownloaderTriggerStateDto
{
    NotAttempted,
    Failed,
    Accepted,
    OutcomeUnknown
}

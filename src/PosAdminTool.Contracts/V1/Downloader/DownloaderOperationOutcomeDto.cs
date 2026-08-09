namespace PosAdminTool.Contracts.V1.Downloader;

/// <summary>
/// Downloader-specific operation truth. Only logical branch codes, stable codes, and opaque
/// artifact capabilities are exposed.
/// </summary>
public sealed record DownloaderOperationOutcomeDto(
    IReadOnlyList<DownloaderBranchOutcomeDto> Branches,
    string? Serial,
    bool TriggerAccepted);

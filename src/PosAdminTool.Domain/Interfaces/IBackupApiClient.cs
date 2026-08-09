namespace PosAdminTool.Domain.Interfaces;

using PosAdminTool.Domain.Models;

public interface IBackupApiClient
{
    Task<DownloaderTriggerResult> TriggerBackupAsync(
        string apiUrl,
        IReadOnlyList<string> branchCodes,
        CancellationToken cancellationToken = default);
}

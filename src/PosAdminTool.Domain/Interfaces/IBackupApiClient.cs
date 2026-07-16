namespace PosAdminTool.Domain.Interfaces;

public interface IBackupApiClient
{
    Task TriggerBackupAsync(string apiUrl, IReadOnlyList<string> branchCodes, CancellationToken cancellationToken = default);
}

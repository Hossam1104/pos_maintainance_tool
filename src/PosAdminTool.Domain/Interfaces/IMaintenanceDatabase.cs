using PosAdminTool.Domain.Models;

namespace PosAdminTool.Domain.Interfaces;

/// <summary>Optional server-side preview seam for bounded branch-reset row counts.</summary>
public interface IMaintenanceDatabasePreview
{
    Task<IReadOnlyList<MaintenanceTableScope>> GetBranchResetScopeAsync(
        AppSettings settings,
        string databaseName,
        string branchCode,
        IReadOnlyList<string> tableNames,
        CancellationToken cancellationToken = default);
}

/// <summary>Optional explicit database/table reset seam used by the Agent maintenance workflow.</summary>
public interface IMaintenanceDatabaseReset
{
    Task ResetBranchDataAsync(
        AppSettings settings,
        string databaseName,
        string branchCode,
        IReadOnlyList<string> tableNames,
        CancellationToken cancellationToken = default);
}

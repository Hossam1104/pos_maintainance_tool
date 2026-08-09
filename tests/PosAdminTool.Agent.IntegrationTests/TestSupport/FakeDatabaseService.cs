using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Agent.IntegrationTests.TestSupport;

/// <summary>Filesystem-only database double used by Agent integration tests; it never invokes SQL.</summary>
public sealed class FakeDatabaseService : IDatabaseService, IDatabaseRestoreVerifier, IMaintenanceDatabasePreview, IMaintenanceDatabaseReset
{
    public List<(string DatabaseName, bool UseCompatibilityMode)> BackupCalls { get; } = [];

    public List<(string DatabaseName, IReadOnlyList<RestoreFileInfo> LogicalFiles, string DbFilesPath)> RestoreCalls { get; } = [];

    public IReadOnlyList<RestoreFileInfo> RestoreFileList { get; set; } = [];

    public Exception? RestoreFailure { get; set; }

    public bool RestoreVerificationResult { get; set; } = true;

    public bool BlockVerification { get; set; }

    public bool RestoreAttempted { get; private set; }

    public bool RestoreCompleted { get; private set; }

    public bool BranchExistsResult { get; set; } = true;

    public List<(string DatabaseName, string BranchCode, IReadOnlyList<string> Tables)> ResetCalls { get; } = [];

    public IReadOnlyList<MaintenanceTableScope> BranchResetScope { get; set; } = [];

    public Exception? ResetFailure { get; set; }

    public bool BlockReset { get; set; }

    public bool ResetAttempted { get; private set; }

    public bool ResetCompleted { get; private set; }

    public TaskCompletionSource ResetInvocationStarted { get; private set; } = NewSignal();

    public TaskCompletionSource ResetRelease { get; private set; } = NewSignal();

    public TaskCompletionSource RestoreInvocationStarted { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource RestoreVerificationStarted { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource RestoreVerificationRelease { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void ResetRestoreState()
    {
        RestoreFailure = null;
        RestoreVerificationResult = true;
        BlockVerification = false;
        RestoreAttempted = false;
        RestoreCompleted = false;
        RestoreCalls.Clear();
        RestoreInvocationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RestoreVerificationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RestoreVerificationRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ResetFailure = null;
        BranchExistsResult = true;
        BlockReset = false;
        ResetAttempted = false;
        ResetCompleted = false;
        ResetCalls.Clear();
        BranchResetScope = [];
        ResetInvocationStarted = NewSignal();
        ResetRelease = NewSignal();
    }

    public Task TestConnectionAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> BranchExistsAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default) => Task.FromResult(BranchExistsResult);

    public Task<IReadOnlyList<MaintenanceTableScope>> GetBranchResetScopeAsync(
        AppSettings settings,
        string databaseName,
        string branchCode,
        IReadOnlyList<string> tableNames,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MaintenanceTableScope>>(BranchResetScope.Count > 0
            ? BranchResetScope
            : tableNames.Select(table => new MaintenanceTableScope(table, null)).ToList());

    public async Task ResetBranchDataAsync(
        AppSettings settings,
        string databaseName,
        string branchCode,
        IReadOnlyList<string> tableNames,
        CancellationToken cancellationToken = default)
    {
        ResetAttempted = true;
        ResetInvocationStarted.TrySetResult();
        if (BlockReset) await ResetRelease.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (ResetFailure is not null) throw ResetFailure;
        ResetCalls.Add((databaseName, branchCode, tableNames));
        ResetCompleted = true;
    }

    public Task ResetBranchDataAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task BackupDatabaseAsync(
        AppSettings settings,
        string databaseName,
        string backupFilePath,
        bool useCompatibilityMode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BackupCalls.Add((databaseName, useCompatibilityMode));
        File.WriteAllText(backupFilePath, $"integration-test-backup:{databaseName}");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RestoreFileInfo>> ReadRestoreFileListAsync(
        AppSettings settings,
        string backupFilePath,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(RestoreFileList);

    public Task RestoreDatabaseAsync(
        AppSettings settings,
        string targetDatabase,
        string backupFilePath,
        IReadOnlyList<RestoreFileInfo> logicalFiles,
        string dbFilesPath,
        CancellationToken cancellationToken = default)
    {
        RestoreAttempted = true;
        RestoreInvocationStarted.TrySetResult();
        cancellationToken.ThrowIfCancellationRequested();
        if (RestoreFailure is not null) return Task.FromException(RestoreFailure);
        RestoreCalls.Add((targetDatabase, logicalFiles, dbFilesPath));
        RestoreCompleted = true;
        return Task.CompletedTask;
    }

    public Task<bool> VerifyRestoreAsync(
        AppSettings settings,
        string targetDatabase,
        CancellationToken cancellationToken = default)
    {
        RestoreVerificationStarted.TrySetResult();
        return VerifyRestoreCoreAsync();
    }

    private async Task<bool> VerifyRestoreCoreAsync()
    {
        if (BlockVerification) await RestoreVerificationRelease.Task.ConfigureAwait(false);
        return RestoreVerificationResult;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Agent.IntegrationTests.TestSupport;

/// <summary>Filesystem-only database double used by Agent integration tests; it never invokes SQL.</summary>
public sealed class FakeDatabaseService : IDatabaseService, IDatabaseRestoreVerifier
{
    public List<(string DatabaseName, bool UseCompatibilityMode)> BackupCalls { get; } = [];

    public List<(string DatabaseName, IReadOnlyList<RestoreFileInfo> LogicalFiles, string DbFilesPath)> RestoreCalls { get; } = [];

    public IReadOnlyList<RestoreFileInfo> RestoreFileList { get; set; } = [];

    public Exception? RestoreFailure { get; set; }

    public bool RestoreVerificationResult { get; set; } = true;

    public Task TestConnectionAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> BranchExistsAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default) => Task.FromResult(true);

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
        cancellationToken.ThrowIfCancellationRequested();
        if (RestoreFailure is not null) return Task.FromException(RestoreFailure);
        RestoreCalls.Add((targetDatabase, logicalFiles, dbFilesPath));
        return Task.CompletedTask;
    }

    public Task<bool> VerifyRestoreAsync(
        AppSettings settings,
        string targetDatabase,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(RestoreVerificationResult);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

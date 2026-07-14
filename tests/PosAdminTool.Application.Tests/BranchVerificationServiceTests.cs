using PosAdminTool.Application.Services;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Application.Tests;

public sealed class BranchVerificationServiceTests
{
    [Fact]
    public async Task VerifyAsyncRejectsEmptyBranchBeforeDatabaseCall()
    {
        var service = new BranchVerificationService(new ThrowingDatabaseService());

        var result = await service.VerifyAsync(new AppSettings { BranchCode = "   " });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class ThrowingDatabaseService : IDatabaseService
    {
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public Task TestConnectionAsync(AppSettings settings, ClientDbConfig? overrideConnection = null, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Database should not be called.");
        }

        public Task<bool> BranchExistsAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Database should not be called.");
        }

        public Task ResetBranchDataAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Database should not be called.");
        }

        public Task<IReadOnlyList<string>> QueryRandomScannedCodesAsync(ClientDbConfig config, int count, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Database should not be called.");
        }

        public Task BackupDatabaseAsync(AppSettings settings, string databaseName, string backupFilePath, bool useCompatibilityMode, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Database should not be called.");
        }

        public Task<IReadOnlyList<RestoreFileInfo>> ReadRestoreFileListAsync(AppSettings settings, string backupFilePath, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Database should not be called.");
        }

        public Task RestoreDatabaseAsync(AppSettings settings, string targetDatabase, string backupFilePath, IReadOnlyList<RestoreFileInfo> logicalFiles, string dbFilesPath, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Database should not be called.");
        }
    }
}

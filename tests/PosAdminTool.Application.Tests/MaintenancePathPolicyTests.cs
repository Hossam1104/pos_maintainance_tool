using System.Collections.Concurrent;
using PosAdminTool.Application.Maintenance;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Application.Tests;

public sealed class MaintenancePathPolicyTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pos-maintenance-policy-root");
    private readonly FakeFileSystem _fileSystem = new();

    [Fact]
    public void ContainmentAndRootPolicyRejectTraversalAbsoluteProtectedInstallAndRootTargets()
    {
        var settings = PathSettings();
        var policy = new MaintenancePathPolicy(_fileSystem);

        Assert.Equal(MaintenanceFailureCodes.OutsideManagedRoot, policy.Resolve("one", Path.Combine(_root, "..", "outside"), settings).RejectionCode);
        Assert.Equal(MaintenanceFailureCodes.OutsideManagedRoot, policy.Resolve("two", Path.Combine(Path.GetTempPath(), "unmanaged"), settings).RejectionCode);
        Assert.Equal(MaintenanceFailureCodes.ProtectedRoot, policy.Resolve("three", Path.Combine(_root, "protected", "child"), settings).RejectionCode);
        Assert.Equal(MaintenanceFailureCodes.InstallRoot, policy.Resolve("four", Path.Combine(_root, "install", "child"), settings).RejectionCode);
        Assert.Equal(MaintenanceFailureCodes.RootTarget, policy.Resolve("five", _root, settings).RejectionCode);
    }

    [Fact]
    public void DriveRelativeUncAndUnresolvedEnvironmentPathsFailClosed()
    {
        var settings = PathSettings();
        var policy = new MaintenancePathPolicy(_fileSystem);

        Assert.Equal(MaintenanceFailureCodes.DriveRelativePath, policy.Resolve("drive", "C:relative", settings).RejectionCode);
        Assert.Equal(MaintenanceFailureCodes.UncNotAllowed, policy.Resolve("unc", @"\\server\share\folder", settings).RejectionCode);
        Assert.Equal(MaintenanceFailureCodes.UnresolvedEnvironmentVariable, policy.Resolve("env", "%POS_MISSING_MAINTENANCE_ROOT%\target", settings).RejectionCode);

        settings.AllowUncPaths = true;
        Assert.Equal(MaintenanceFailureCodes.OutsideManagedRoot, policy.Resolve("unc-allowed", @"\\server\share\folder", settings).RejectionCode);
    }

    [Fact]
    public void EnvironmentExpansionThatEscapesManagedRootIsRejected()
    {
        var settings = PathSettings();
        settings.ManagedRoots = ["%TEMP%\\" + Path.GetFileName(_root)];
        var policy = new MaintenancePathPolicy(_fileSystem);

        var result = policy.Resolve("escape", "%TEMP%\\outside", settings);

        Assert.Equal(MaintenanceFailureCodes.OutsideManagedRoot, result.RejectionCode);
    }

    [Fact]
    public void InvalidPolicyRootsFailClosedInsteadOfBeingIgnored()
    {
        var settings = PathSettings();
        settings.ProtectedRoots = [Path.Combine(_root, "protected"), "C:relative-protected"];
        var policy = new MaintenancePathPolicy(_fileSystem);

        var result = policy.Resolve("invalid-policy", Path.Combine(_root, "target"), settings);

        Assert.Equal(MaintenanceFailureCodes.InvalidConfiguration, result.RejectionCode);
    }

    [Fact]
    public void ReparseAndSymlinkEscapesAreRejectedEvenWhenTheLinkNameIsUnderTheManagedRoot()
    {
        var settings = PathSettings();
        var target = Path.Combine(_root, "link");
        _fileSystem.SetReparse(target, Path.Combine(Path.GetTempPath(), "outside-link-target"));
        _fileSystem.SetAncestors(target, [_fileSystem.Inspect(target)]);
        var policy = new MaintenancePathPolicy(_fileSystem);

        var rejected = policy.Resolve("link", target, settings);
        Assert.Equal(MaintenanceFailureCodes.ReparsePoint, rejected.RejectionCode);

        settings.RejectReparsePoints = false;
        var escaped = policy.Resolve("link", target, settings);
        Assert.Equal(MaintenanceFailureCodes.ReparseEscape, escaped.RejectionCode);
    }

    [Fact]
    public async Task CleanupRecomputesTheTargetAndFailsClosedWhenConfigurationChanges()
    {
        var settings = ApplicationSettings();
        settings.Maintenance.CleanupTargets = [Path.Combine(_root, "first")];
        _fileSystem.SetEntry(settings.Maintenance.CleanupTargets[0]);
        var service = new MaintenanceService(new FakeDatabase(), new FakeServices(), _fileSystem);
        var preview = await service.BuildCleanupPreviewAsync(settings);

        settings.Maintenance.CleanupTargets = [Path.Combine(_root, "changed")];
        _fileSystem.SetEntry(settings.Maintenance.CleanupTargets[0]);
        var execution = await service.ExecuteCleanupAsync(settings, preview.Intent!.Fingerprint);

        Assert.Equal(OperationStatus.Failed, execution.Operation.Status);
        Assert.Equal(MaintenanceFailureCodes.PreviewChanged, execution.FailureCode);
        Assert.Empty(_fileSystem.DeleteCalls);
    }

    [Fact]
    public async Task CleanupRecordsPartialResidueAfterADeleteFailure()
    {
        var settings = ApplicationSettings();
        settings.Maintenance.CleanupTargets = [Path.Combine(_root, "first"), Path.Combine(_root, "second")];
        _fileSystem.SetEntry(settings.Maintenance.CleanupTargets[0]);
        _fileSystem.SetEntry(settings.Maintenance.CleanupTargets[1]);
        _fileSystem.DeleteFailure = new IOException("private path and secret connection string");
        var service = new MaintenanceService(new FakeDatabase(), new FakeServices(), _fileSystem);

        var execution = await service.ExecuteCleanupAsync(settings);

        Assert.Equal(OperationStatus.PartialSuccess, execution.Operation.Status);
        Assert.True(execution.Evidence.RecoveryRequired);
        Assert.Contains(execution.Evidence.Items, item => item.Attempted && item.ResidueUncertain && item.FailureCode == MaintenanceFailureCodes.TargetDeleteFailed);
        Assert.DoesNotContain("private path", string.Join(" ", execution.Operation.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BranchResetUsesConfiguredLogicalBranchWithoutBareBakParsing()
    {
        var settings = ApplicationSettings();
        settings.BranchCode = "NORTH_EU_01";
        settings.Maintenance.BranchResetDatabase = "BranchData";
        var database = new FakeDatabase();
        var service = new MaintenanceService(database, new FakeServices(), _fileSystem);

        var preview = await service.BuildBranchResetPreviewAsync(settings);
        var execution = await service.ExecuteBranchResetAsync(settings, preview.Intent!.Fingerprint);

        Assert.True(preview.Ready);
        Assert.Equal("RESET BRANCH NORTH_EU_01", preview.Intent!.ConfirmationText);
        Assert.Equal(OperationStatus.Success, execution.Operation.Status);
        Assert.Equal("BranchData", database.ResetCalls.Single().DatabaseName);
        Assert.Equal("NORTH_EU_01", database.ResetCalls.Single().BranchCode);
    }

    [Fact]
    public async Task CancellationBeforeTheResetSeamIsCancelledWithoutDestructiveAttempt()
    {
        var settings = ApplicationSettings();
        settings.BranchCode = "NORTH_EU_02";
        settings.Maintenance.BranchResetDatabase = "BranchData";
        var database = new FakeDatabase();
        var service = new MaintenanceService(database, new FakeServices(), _fileSystem);
        var preview = await service.BuildBranchResetPreviewAsync(settings);
        using var cancellation = new CancellationTokenSource();
        database.OnBranchExists = cancellation.Cancel;

        var execution = await service.ExecuteBranchResetAsync(
            settings,
            preview.Intent!.Fingerprint,
            cancellationToken: cancellation.Token);

        Assert.Equal(OperationStatus.Cancelled, execution.Operation.Status);
        Assert.False(execution.Evidence.DestructiveAttempted);
        Assert.Empty(execution.Evidence.Items);
        Assert.Empty(database.ResetCalls);
    }

    private MaintenanceSettings PathSettings()
    {
        return new MaintenanceSettings
        {
            ManagedRoots = [_root],
            DataRoots = [_root],
            ProtectedRoots = [Path.Combine(_root, "protected")],
            InstallRoots = [Path.Combine(_root, "install")],
        };
    }

    private AppSettings ApplicationSettings() => new()
    {
        BranchCode = "NORTH_EU_01",
        Services = ["TestService"],
        Databases = ["BranchData"],
        Maintenance = PathSettings(),
    };

    private sealed class FakeFileSystem : IMaintenanceFileSystem
    {
        private readonly ConcurrentDictionary<string, MaintenancePathInspection> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, IReadOnlyList<MaintenancePathInspection>> _ancestorEntries = new(StringComparer.OrdinalIgnoreCase);

        public ConcurrentQueue<string> DeleteCalls { get; } = new();
        public Exception? DeleteFailure { get; set; }
        public string ExpandEnvironmentVariables(string path) => Environment.ExpandEnvironmentVariables(path);
        public string GetFullPath(string path) => Path.GetFullPath(path);
        public MaintenancePathInspection Inspect(string path) => _entries.GetValueOrDefault(GetFullPath(path), new(GetFullPath(path), false, false, false, null, null, null));
        public IReadOnlyList<MaintenancePathInspection> InspectAncestors(string path) => _ancestorEntries.GetValueOrDefault(GetFullPath(path), [Inspect(path)]);
        public long? TryGetAvailableFreeSpace(string path) => 1024;
        public Task DeleteAsync(string path, bool recursive, CancellationToken cancellationToken = default)
        {
            DeleteCalls.Enqueue(path);
            if (DeleteFailure is not null) return Task.FromException(DeleteFailure);
            return Task.CompletedTask;
        }
        public void SetEntry(string path) => _entries[GetFullPath(path)] = new(GetFullPath(path), true, true, false, null, null, 1);
        public void SetReparse(string path, string target) => _entries[GetFullPath(path)] = new(GetFullPath(path), true, true, true, target, null, 1);
        public void SetAncestors(string path, IReadOnlyList<MaintenancePathInspection> entries) => _ancestorEntries[GetFullPath(path)] = entries;
    }

    private sealed class FakeServices : IServiceManager
    {
        public Task<ServiceStatus> GetStatusAsync(string serviceName, CancellationToken cancellationToken = default) => Task.FromResult(ServiceStatus.Running);
        public Task<IReadOnlyDictionary<string, ServiceStatus>> GetStatusesAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<string, ServiceStatus>>(serviceNames.ToDictionary(item => item, _ => ServiceStatus.Running));
        public Task ControlAsync(string serviceName, ServiceControlAction action, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDatabase : IDatabaseService, IMaintenanceDatabaseReset
    {
        public List<(string DatabaseName, string BranchCode, IReadOnlyList<string> Tables)> ResetCalls { get; } = [];
        public Action? OnBranchExists { get; set; }
        public Task TestConnectionAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> BranchExistsAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default)
        {
            OnBranchExists?.Invoke();
            return Task.FromResult(true);
        }
        public Task ResetBranchDataAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResetBranchDataAsync(AppSettings settings, string databaseName, string branchCode, IReadOnlyList<string> tableNames, CancellationToken cancellationToken = default)
        {
            ResetCalls.Add((databaseName, branchCode, tableNames));
            return Task.CompletedTask;
        }
        public Task BackupDatabaseAsync(AppSettings settings, string databaseName, string backupFilePath, bool useCompatibilityMode, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<RestoreFileInfo>> ReadRestoreFileListAsync(AppSettings settings, string backupFilePath, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RestoreFileInfo>>([]);
        public Task RestoreDatabaseAsync(AppSettings settings, string targetDatabase, string backupFilePath, IReadOnlyList<RestoreFileInfo> logicalFiles, string dbFilesPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

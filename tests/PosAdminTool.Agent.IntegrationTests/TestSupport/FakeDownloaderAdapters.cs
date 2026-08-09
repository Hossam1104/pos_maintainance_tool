using PosAdminTool.Domain.Exceptions;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Agent.IntegrationTests.TestSupport;

public sealed class FakeDownloaderApiClient : IBackupApiClient
{
    public Exception? TriggerFailure { get; set; }

    public int TriggerCalls { get; private set; }

    public Task TriggerBackupAsync(
        string apiUrl,
        IReadOnlyList<string> branchCodes,
        CancellationToken cancellationToken = default)
    {
        TriggerCalls++;
        if (TriggerFailure is not null) throw TriggerFailure;
        return Task.CompletedTask;
    }

    public void Reset()
    {
        TriggerFailure = null;
        TriggerCalls = 0;
    }
}

public sealed class FakeDownloaderRepository : IBackupRepository
{
    private readonly object _gate = new();

    public List<RemoteEntryInfo> Directories { get; } = [];

    public Dictionary<string, List<RemoteEntryInfo>> FilesByFolder { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Exception? ListDirectoriesFailure { get; set; }

    public Exception? ListFilesFailure { get; set; }

    public HashSet<string> FailedDownloadBranches { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool BlockListDirectories { get; set; }

    public TaskCompletionSource ListDirectoriesStarted { get; private set; } = NewSignal();

    public int ListDirectoriesCalls { get; private set; }

    public int ListFilesCalls { get; private set; }

    public async Task<IReadOnlyList<RemoteEntryInfo>> ListDirectoriesAsync(
        RemoteConnectionInfo connection,
        string rootFolder,
        CancellationToken cancellationToken = default)
    {
        lock (_gate) ListDirectoriesCalls++;
        ListDirectoriesStarted.TrySetResult();
        if (BlockListDirectories)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }

        if (ListDirectoriesFailure is not null) throw ListDirectoriesFailure;
        return Directories;
    }

    public Task<IReadOnlyList<RemoteEntryInfo>> ListFilesAsync(
        RemoteConnectionInfo connection,
        string folder,
        CancellationToken cancellationToken = default)
    {
        lock (_gate) ListFilesCalls++;
        if (ListFilesFailure is not null) throw ListFilesFailure;
        var files = FilesByFolder.TryGetValue(folder, out var value) ? value : [];
        return Task.FromResult((IReadOnlyList<RemoteEntryInfo>)files);
    }

    public Task DownloadFileAsync(
        RemoteConnectionInfo connection,
        string remoteFilePath,
        string localFilePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var branch = Path.GetFileName(remoteFilePath).Split('_')[0];
        if (FailedDownloadBranches.Contains(branch))
        {
            throw new BackupRepositoryException(DownloaderFailureCodes.SmbConnectionFailed);
        }

        var directory = Path.GetDirectoryName(localFilePath);
        if (directory is not null) Directory.CreateDirectory(directory);
        File.WriteAllBytes(localFilePath, [0x50, 0x4b, 0x03, 0x04]);
        progress?.Report(1);
        return Task.CompletedTask;
    }

    public void Reset()
    {
        lock (_gate)
        {
            Directories.Clear();
            FilesByFolder.Clear();
            FailedDownloadBranches.Clear();
            ListDirectoriesFailure = null;
            ListFilesFailure = null;
            BlockListDirectories = false;
            ListDirectoriesCalls = 0;
            ListFilesCalls = 0;
            ListDirectoriesStarted = NewSignal();
        }
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class ImmediateDownloaderDelay : IDownloaderDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

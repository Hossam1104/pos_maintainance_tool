using System.Net;
using System.Net.Http;
using System.Text;
using PosAdminTool.Domain.Exceptions;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;
using PosAdminTool.Infrastructure.Http;

namespace PosAdminTool.Agent.IntegrationTests.TestSupport;

public sealed class FakeDownloaderApiClient : IBackupApiClient
{
    public Exception? TriggerFailure { get; set; }

    public DownloaderTriggerResult? TriggerResult { get; set; }

    // Optional disposable HTTP mode used by the worker regression to exercise the real
    // BackupApiClient dispatch/response boundary without contacting a remote endpoint.
    public HttpStatusCode? RemoteResponseStatus { get; set; }

    public string? RemoteResponseBody { get; set; }

    public int RemoteRequestCount { get; private set; }

    public int TriggerCalls { get; private set; }

    public async Task<DownloaderTriggerResult> TriggerBackupAsync(
        string apiUrl,
        IReadOnlyList<string> branchCodes,
        CancellationToken cancellationToken = default)
    {
        TriggerCalls++;
        if (TriggerFailure is not null) throw TriggerFailure;

        if (RemoteResponseStatus is { } statusCode)
        {
            using var httpClient = new HttpClient(new FixedResponseHandler(
                statusCode,
                RemoteResponseBody,
                () => RemoteRequestCount++));
            var backupApiClient = new BackupApiClient(
                httpClient,
                new FixedHostAddressResolver(IPAddress.Parse("198.51.100.10")));
            return await backupApiClient.TriggerBackupAsync(apiUrl, branchCodes, cancellationToken);
        }

        return TriggerResult ?? new DownloaderTriggerResult(DownloaderTriggerState.Accepted);
    }

    public void Reset()
    {
        TriggerFailure = null;
        TriggerResult = null;
        RemoteResponseStatus = null;
        RemoteResponseBody = null;
        TriggerCalls = 0;
        RemoteRequestCount = 0;
    }

    private sealed class FixedResponseHandler(
        HttpStatusCode statusCode,
        string? responseBody,
        Action onRequest) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            onRequest();
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                RequestMessage = request,
                Content = new StringContent(responseBody ?? string.Empty, Encoding.UTF8, "text/plain"),
            });
        }
    }

    private sealed class FixedHostAddressResolver(IPAddress address) : IHostAddressResolver
    {
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(
            string host,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((IReadOnlyList<IPAddress>)[address]);
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

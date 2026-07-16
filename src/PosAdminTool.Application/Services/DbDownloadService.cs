using System.Text.RegularExpressions;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Application.Services;

public sealed partial class DbDownloadService(IBackupApiClient apiClient, IBackupRepository backupRepository)
{
    public async Task<BackupJob> RunAsync(
        DbDownloaderSettings settings,
        IReadOnlyList<string> branchCodes,
        Action<BranchBackupItem>? onItemChanged = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var job = new BackupJob(branchCodes);
        var connection = new RemoteConnectionInfo(settings.RdbServerIp, settings.RdbUsername, settings.RdbPassword);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(settings.TimeoutSeconds);

        var triggeredAtUtc = DateTimeOffset.UtcNow;
        progress?.Report($"Triggering backup job for {branchCodes.Count} branch(es)...");
        await apiClient.TriggerBackupAsync(settings.ApiUrl, branchCodes, cancellationToken).ConfigureAwait(false);

        var batchFolder = await DiscoverBatchFolderAsync(connection, settings.BackupRootFolder, triggeredAtUtc, deadline, progress, cancellationToken)
            .ConfigureAwait(false);
        if (batchFolder is null)
        {
            progress?.Report("Batch folder was not found within the timeout.");
            foreach (var item in job.Items)
            {
                item.Status = BranchBackupStatus.TimedOut;
                item.ErrorMessage = "Batch folder never appeared.";
                onItemChanged?.Invoke(item);
            }

            return job;
        }

        job.BatchFolderPath = batchFolder.FullPath;
        progress?.Report($"Watching batch folder: {batchFolder.FullPath}");

        var pollInterval = TimeSpan.FromSeconds(settings.PollIntervalSeconds);

        var pending = job.Items.Where(i => i.Status == BranchBackupStatus.Pending).ToList();
        while (pending.Count > 0 && DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            var files = await backupRepository.ListFilesAsync(connection, batchFolder.FullPath, cancellationToken).ConfigureAwait(false);

            foreach (var item in pending.ToList())
            {
                var match = files.FirstOrDefault(f => IsZipForBranch(f.Name, item.BranchCode));
                if (match is null)
                {
                    continue;
                }

                job.Serial ??= ExtractSerial(match.Name);
                item.RemoteZipPath = match.FullPath;
                item.Status = BranchBackupStatus.ZipDetected;
                onItemChanged?.Invoke(item);
                progress?.Report($"{item.BranchCode}: zip detected ({match.Name})");

                var isStable = await IsFileStableAsync(connection, match, cancellationToken).ConfigureAwait(false);
                item.Status = BranchBackupStatus.Validating;
                onItemChanged?.Invoke(item);

                if (isStable)
                {
                    item.Status = BranchBackupStatus.Ready;
                    onItemChanged?.Invoke(item);
                    progress?.Report($"{item.BranchCode}: ready for download");
                    pending.Remove(item);
                }
            }

            if (pending.Count > 0)
            {
                await Task.Delay(pollInterval, CancellationToken.None).ConfigureAwait(false);
            }
        }

        foreach (var item in pending)
        {
            item.Status = BranchBackupStatus.TimedOut;
            item.ErrorMessage = "Zip file did not appear before the timeout.";
            onItemChanged?.Invoke(item);
            progress?.Report($"{item.BranchCode}: timed out waiting for zip");
        }

        return job;
    }

    public async Task DownloadAsync(
        DbDownloaderSettings settings,
        BranchBackupItem item,
        string localFolder,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (item.RemoteZipPath is null)
        {
            throw new InvalidOperationException($"Branch {item.BranchCode} has no remote zip path yet.");
        }

        var connection = new RemoteConnectionInfo(settings.RdbServerIp, settings.RdbUsername, settings.RdbPassword);
        Directory.CreateDirectory(localFolder);
        var localPath = Path.Combine(localFolder, Path.GetFileName(item.RemoteZipPath));

        item.Status = BranchBackupStatus.Downloading;
        try
        {
            await backupRepository.DownloadFileAsync(connection, item.RemoteZipPath, localPath, progress, cancellationToken).ConfigureAwait(false);
            item.LocalDownloadPath = localPath;
            item.Status = BranchBackupStatus.Downloaded;
        }
        catch (Exception ex)
        {
            item.Status = BranchBackupStatus.Failed;
            item.ErrorMessage = ex.Message;
            throw;
        }
    }

    private async Task<RemoteEntryInfo?> DiscoverBatchFolderAsync(
        RemoteConnectionInfo connection,
        string rootFolder,
        DateTimeOffset triggeredAtUtc,
        DateTimeOffset deadline,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            var directories = await backupRepository.ListDirectoriesAsync(connection, rootFolder, cancellationToken).ConfigureAwait(false);
            var candidates = directories
                .Where(d => d.CreatedAtUtc >= triggeredAtUtc.AddSeconds(-5))
                .OrderByDescending(d => d.CreatedAtUtc)
                .ToList();

            if (candidates.Count > 1)
            {
                progress?.Report($"Warning: {candidates.Count} new folders appeared since the job was triggered; using the most recent.");
            }

            if (candidates.Count > 0)
            {
                return candidates[0];
            }

            await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
        }

        return null;
    }

    private async Task<bool> IsFileStableAsync(RemoteConnectionInfo connection, RemoteEntryInfo file, CancellationToken cancellationToken)
    {
        var firstSize = file.SizeBytes;
        await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
        var refreshed = await backupRepository.ListFilesAsync(connection, Path.GetDirectoryName(file.FullPath) ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);
        var current = refreshed.FirstOrDefault(f => f.FullPath == file.FullPath);
        return current is not null && current.SizeBytes == firstSize && current.SizeBytes > 0;
    }

    private static bool IsZipForBranch(string fileName, string branchCode)
    {
        return fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            && fileName.StartsWith(branchCode + "_", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractSerial(string fileName)
    {
        var match = SerialRegex().Match(fileName);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"_(\d+)\.zip$", RegexOptions.IgnoreCase)]
    private static partial Regex SerialRegex();
}

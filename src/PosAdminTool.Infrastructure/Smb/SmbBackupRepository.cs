using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Infrastructure.Smb;

/// <summary>
/// First <see cref="IBackupRepository"/> provider: reads the remote backup folder over an
/// SMB/UNC administrative share (e.g. \\server\D$\DbBackups). All paths passed in and out
/// use the server's own local-path form (e.g. D:\DbBackups\40760799) so callers stay agnostic
/// of the SMB translation; a future HTTP-based provider can implement the same contract.
/// </summary>
public sealed class SmbBackupRepository : IBackupRepository
{
    public Task<IReadOnlyList<RemoteEntryInfo>> ListDirectoriesAsync(RemoteConnectionInfo connection, string rootFolder, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                var uncRoot = SmbPathResolver.ToUncPath(connection.ServerIp, rootFolder);
                using var scope = SmbConnectionScope.Connect(uncRoot, connection.Username, connection.Password);

                if (!Directory.Exists(uncRoot))
                {
                    return (IReadOnlyList<RemoteEntryInfo>)[];
                }

                var entries = Directory.GetDirectories(uncRoot)
                    .Select(path => ToEntryInfo(rootFolder, uncRoot, path, isDirectory: true))
                    .ToList();
                return (IReadOnlyList<RemoteEntryInfo>)entries;
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<RemoteEntryInfo>> ListFilesAsync(RemoteConnectionInfo connection, string folder, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                var uncFolder = SmbPathResolver.ToUncPath(connection.ServerIp, folder);
                using var scope = SmbConnectionScope.Connect(uncFolder, connection.Username, connection.Password);

                if (!Directory.Exists(uncFolder))
                {
                    return (IReadOnlyList<RemoteEntryInfo>)[];
                }

                var entries = Directory.GetFiles(uncFolder)
                    .Select(path => ToEntryInfo(folder, uncFolder, path, isDirectory: false))
                    .ToList();
                return (IReadOnlyList<RemoteEntryInfo>)entries;
            },
            cancellationToken);
    }

    public async Task DownloadFileAsync(RemoteConnectionInfo connection, string remoteFilePath, string localFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var uncFile = SmbPathResolver.ToUncPath(connection.ServerIp, remoteFilePath);
        using var scope = SmbConnectionScope.Connect(SmbPathResolver.ToUncPath(connection.ServerIp, Path.GetPathRoot(remoteFilePath) ?? remoteFilePath), connection.Username, connection.Password);

        var tempPath = localFilePath + ".partial";
        await using (var source = new FileStream(uncFile, FileMode.Open, FileAccess.Read, FileShare.Read))
        await using (var destination = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var totalBytes = source.Length;
            var buffer = new byte[81920];
            long copied = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                copied += read;
                if (totalBytes > 0)
                {
                    progress?.Report((double)copied / totalBytes);
                }
            }
        }

        File.Move(tempPath, localFilePath, overwrite: true);
    }

    private static RemoteEntryInfo ToEntryInfo(string logicalParent, string uncParent, string uncPath, bool isDirectory)
    {
        var name = Path.GetFileName(uncPath.TrimEnd('\\'));
        var logicalPath = Path.Combine(logicalParent, name);
        var createdAtUtc = isDirectory
            ? Directory.GetCreationTimeUtc(uncPath)
            : File.GetCreationTimeUtc(uncPath);
        var sizeBytes = isDirectory ? 0 : new FileInfo(uncPath).Length;

        return new RemoteEntryInfo(name, logicalPath, new DateTimeOffset(createdAtUtc, TimeSpan.Zero), sizeBytes);
    }
}

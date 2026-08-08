namespace PosAdminTool.Infrastructure.Smb;

public static class SmbPathResolver
{
    /// <summary>
    /// Maps a local path on the remote server (e.g. <c>D:\DbBackups</c>) to its UNC equivalent
    /// via the server's administrative drive share (e.g. <c>\\192.0.2.10\D$\DbBackups</c>).
    /// </summary>
    public static string ToUncPath(string serverIp, string remoteLocalPath)
    {
        if (string.IsNullOrWhiteSpace(remoteLocalPath) || remoteLocalPath.Length < 2 || remoteLocalPath[1] != ':')
        {
            throw new ArgumentException($"Expected a drive-rooted path (e.g. D:\\DbBackups), got '{remoteLocalPath}'.", nameof(remoteLocalPath));
        }

        var driveLetter = char.ToUpperInvariant(remoteLocalPath[0]);
        var remainder = remoteLocalPath.Length > 2 ? remoteLocalPath[2..].TrimStart('\\') : string.Empty;

        return string.IsNullOrEmpty(remainder)
            ? $@"\\{serverIp}\{driveLetter}$"
            : $@"\\{serverIp}\{driveLetter}$\{remainder}";
    }
}

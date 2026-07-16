namespace PosAdminTool.Domain.Models;

public sealed class DbDownloaderSettings
{
    public string ApiUrl { get; set; } = "http://10.10.9.181:8080/rmsmainserverApi/api/Updates/CreateDbBackupUpdate";

    public string RdbServerIp { get; set; } = string.Empty;

    public string RdbUsername { get; set; } = string.Empty;

    public string RdbPassword { get; set; } = string.Empty;

    public string BackupRootFolder { get; set; } = @"D:\DbBackups";

    public List<string> KnownBranchCodes { get; set; } = [];

    public int PollIntervalSeconds { get; set; } = 5;

    public int TimeoutSeconds { get; set; } = 1800;

    public DbDownloaderSettings Clone()
    {
        return new DbDownloaderSettings
        {
            ApiUrl = ApiUrl,
            RdbServerIp = RdbServerIp,
            RdbUsername = RdbUsername,
            RdbPassword = RdbPassword,
            BackupRootFolder = BackupRootFolder,
            KnownBranchCodes = [.. KnownBranchCodes],
            PollIntervalSeconds = PollIntervalSeconds,
            TimeoutSeconds = TimeoutSeconds
        };
    }
}

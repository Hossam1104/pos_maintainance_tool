namespace PosAdminTool.Domain.Models;

/// <summary>
/// Service-owned, non-secret Agent configuration (plan section 5.5). Deliberately separate from the
/// legacy <see cref="AppSettings"/> model: no SQL or RDB password field exists here at all, so a
/// secret can never leak into this type by accident. Secrets live only in <c>IAgentSecretStore</c>.
/// </summary>
public sealed class AgentConfiguration
{
    public string SqlInstance { get; set; } = string.Empty;

    public string SqlUser { get; set; } = string.Empty;

    public string BranchCode { get; set; } = string.Empty;

    public string PosNumber { get; set; } = string.Empty;

    public string Release { get; set; } = string.Empty;

    public string ClientName { get; set; } = string.Empty;

    public string ApiBaseUrl { get; set; } = string.Empty;

    public string BackupFolder { get; set; } = string.Empty;

    // These are service-owned non-secret source locations. They are intentionally omitted from
    // the browser DTO; backup requests select one of the three stable component IDs instead.
    public string BranchConfigPath { get; set; } = string.Empty;

    public string CashierGrpcConfigPath { get; set; } = string.Empty;

    public string CashierUiConfigPath { get; set; } = string.Empty;

    public List<string> Databases { get; set; } = [];

    public List<string> Services { get; set; } = [];

    public AgentDownloaderConfiguration Downloader { get; set; } = new();

    /// <summary>Optimistic-concurrency token. Incremented on every persisted mutation, including
    /// secret-only changes, so a client's last read always reflects whether anything changed.</summary>
    public long Version { get; set; }

    public AgentConfiguration Clone()
    {
        return new AgentConfiguration
        {
            SqlInstance = SqlInstance,
            SqlUser = SqlUser,
            BranchCode = BranchCode,
            PosNumber = PosNumber,
            Release = Release,
            ClientName = ClientName,
            ApiBaseUrl = ApiBaseUrl,
            BackupFolder = BackupFolder,
            BranchConfigPath = BranchConfigPath,
            CashierGrpcConfigPath = CashierGrpcConfigPath,
            CashierUiConfigPath = CashierUiConfigPath,
            Databases = [.. Databases],
            Services = [.. Services],
            Downloader = Downloader.Clone(),
            Version = Version
        };
    }
}

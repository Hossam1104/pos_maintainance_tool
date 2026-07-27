namespace PosAdminTool.Domain.Models;

/// <summary>
/// Non-secret DB Downloader settings owned by the Agent. The RDB password never lives here — it is
/// held only by <c>IAgentSecretStore</c> (plan section 5.5).
/// </summary>
public sealed class AgentDownloaderConfiguration
{
    public string ApiUrl { get; set; } = string.Empty;

    public string RdbServerIp { get; set; } = string.Empty;

    public string RdbUsername { get; set; } = string.Empty;

    public List<string> KnownBranchCodes { get; set; } = [];

    public int PollIntervalSeconds { get; set; } = 5;

    public int TimeoutSeconds { get; set; } = 1800;

    public AgentDownloaderConfiguration Clone()
    {
        return new AgentDownloaderConfiguration
        {
            ApiUrl = ApiUrl,
            RdbServerIp = RdbServerIp,
            RdbUsername = RdbUsername,
            KnownBranchCodes = [.. KnownBranchCodes],
            PollIntervalSeconds = PollIntervalSeconds,
            TimeoutSeconds = TimeoutSeconds
        };
    }
}

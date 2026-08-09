using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Agent.Configuration;

/// <summary>
/// Compatibility port used solely by retained configuration use cases. It maps their legacy shape
/// to the Agent-owned store and secret vault, so they never write a user-profile config file.
/// </summary>
public sealed class AgentLegacyConfigurationService(
    IAgentConfigurationStore configurations,
    IAgentSecretStore secrets) : IConfigurationService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string ConfigFilePath => "agent-owned";
    public string? LastLoadError => null;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var config = await configurations.LoadAsync(cancellationToken).ConfigureAwait(false);
        return await ToLegacyAsync(config, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await configurations.LoadAsync(cancellationToken).ConfigureAwait(false);
            current.SqlInstance = settings.SqlInstance;
            current.SqlUser = settings.SqlUser;
            current.BranchCode = settings.BranchCode;
            current.PosNumber = settings.PosNumber;
            current.Release = settings.Release;
            current.ClientName = settings.ClientName;
            current.ApiBaseUrl = settings.ApiBaseUrl;
            current.BackupFolder = settings.BackupFolder;
            current.DbFilesPath = settings.DbFilesPath;
            current.BranchConfigPath = settings.BranchConfigPath;
            current.CashierGrpcConfigPath = settings.CashierGrpcConfigPath;
            current.CashierUiConfigPath = settings.CashierUiConfigPath;
            current.Databases = [.. settings.Databases];
            current.Services = [.. settings.Services];
            current.Downloader.ApiUrl = settings.DbDownloader.ApiUrl;
            current.Downloader.RdbServerIp = settings.DbDownloader.RdbServerIp;
            current.Downloader.RdbUsername = settings.DbDownloader.RdbUsername;
            current.Downloader.KnownBranchCodes = [.. settings.DbDownloader.KnownBranchCodes];
            current.Downloader.PollIntervalSeconds = settings.DbDownloader.PollIntervalSeconds;
            current.Downloader.TimeoutSeconds = settings.DbDownloader.TimeoutSeconds;
            current.Version++;
            await configurations.SaveAsync(current, cancellationToken).ConfigureAwait(false);

            // A legacy import may omit passwords. Omission must retain the service-owned secret.
            if (!string.IsNullOrWhiteSpace(settings.SqlPassword))
                await secrets.SetSecretAsync(AgentSecretKind.SqlPassword, settings.SqlPassword, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(settings.DbDownloader.RdbPassword))
                await secrets.SetSecretAsync(AgentSecretKind.RdbPassword, settings.DbDownloader.RdbPassword, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<AppSettings> UpdateAsync(Action<AppSettings> modifier, CancellationToken cancellationToken = default)
    {
        var settings = await LoadAsync(cancellationToken).ConfigureAwait(false);
        modifier(settings);
        await SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        return settings;
    }

    private async Task<AppSettings> ToLegacyAsync(AgentConfiguration config, CancellationToken cancellationToken)
    {
        return new AppSettings
        {
            SqlInstance = config.SqlInstance,
            SqlUser = config.SqlUser,
            SqlPassword = await secrets.TryGetSecretAsync(AgentSecretKind.SqlPassword, cancellationToken).ConfigureAwait(false) ?? string.Empty,
            BranchCode = config.BranchCode,
            PosNumber = config.PosNumber,
            Release = config.Release,
            ClientName = config.ClientName,
            ApiBaseUrl = config.ApiBaseUrl,
            BackupFolder = config.BackupFolder,
            DbFilesPath = string.IsNullOrWhiteSpace(config.DbFilesPath)
                ? new AppSettings().DbFilesPath
                : config.DbFilesPath,
            BranchConfigPath = string.IsNullOrWhiteSpace(config.BranchConfigPath)
                ? new AppSettings().BranchConfigPath
                : config.BranchConfigPath,
            CashierGrpcConfigPath = string.IsNullOrWhiteSpace(config.CashierGrpcConfigPath)
                ? new AppSettings().CashierGrpcConfigPath
                : config.CashierGrpcConfigPath,
            CashierUiConfigPath = string.IsNullOrWhiteSpace(config.CashierUiConfigPath)
                ? new AppSettings().CashierUiConfigPath
                : config.CashierUiConfigPath,
            Databases = [.. config.Databases],
            Services = [.. config.Services],
            DbDownloader = new DbDownloaderSettings
            {
                ApiUrl = config.Downloader.ApiUrl,
                RdbServerIp = config.Downloader.RdbServerIp,
                RdbUsername = config.Downloader.RdbUsername,
                RdbPassword = await secrets.TryGetSecretAsync(AgentSecretKind.RdbPassword, cancellationToken).ConfigureAwait(false) ?? string.Empty,
                KnownBranchCodes = [.. config.Downloader.KnownBranchCodes],
                PollIntervalSeconds = config.Downloader.PollIntervalSeconds,
                TimeoutSeconds = config.Downloader.TimeoutSeconds,
            }
        };
    }
}

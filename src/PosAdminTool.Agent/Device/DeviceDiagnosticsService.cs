using System.Net.Sockets;
using PosAdminTool.Application.Services;
using PosAdminTool.Application.UseCases;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Device;
using PosAdminTool.Domain.Interfaces;

namespace PosAdminTool.Agent.Device;

/// <summary>Produces bounded, redacted diagnostic evidence. In particular, a main-server TCP
/// check is explicitly reachability evidence, never an assertion about RMS application health.</summary>
public sealed class DeviceDiagnosticsService(
    IAgentConfigurationStore configurations,
    IConfigurationService legacyConfiguration,
    TestConnectionUseCase testConnection,
    BranchVerificationService branchVerification,
    TimeProvider clock)
{
    public async Task<DeviceIdentityDto> GetIdentityAsync(CancellationToken cancellationToken) {
        var c = await configurations.LoadAsync(cancellationToken).ConfigureAwait(false);
        return new(c.BranchCode, c.PosNumber, c.Release, c.ClientName);
    }

    public async Task<EvidenceDto> TestDatabaseAsync(CancellationToken cancellationToken) {
        var result = await testConnection.ExecuteAsync(await legacyConfiguration.LoadAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        return Evidence(result.Status == PosAdminTool.Domain.Enums.OperationStatus.Success, "SQL query completed", "SQL connection test failed");
    }

    public async Task<EvidenceDto> VerifyBranchAsync(CancellationToken cancellationToken) {
        var result = await branchVerification.VerifyAsync(await legacyConfiguration.LoadAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        return Evidence(result.Status == PosAdminTool.Domain.Enums.OperationStatus.Success, "Branch exists", "Branch verification failed");
    }

    public async Task<DeviceConnectivityDto> GetConnectivityAsync(CancellationToken cancellationToken) {
        var sql = await TestDatabaseAsync(cancellationToken).ConfigureAwait(false);
        var config = await configurations.LoadAsync(cancellationToken).ConfigureAwait(false);
        var main = await CheckTcpAsync(config.ApiBaseUrl, cancellationToken).ConfigureAwait(false);
        return new(sql, main);
    }

    private async Task<EvidenceDto> CheckTcpAsync(string url, CancellationToken cancellationToken) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return new(FreshnessState.Unknown, null, "Main-server address is not configured");
        try {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var client = new TcpClient();
            await client.ConnectAsync(uri.Host, uri.IsDefaultPort ? (uri.Scheme == "https" ? 443 : 80) : uri.Port, timeout.Token).ConfigureAwait(false);
            return Evidence(true, "TCP endpoint reachable; application health not checked", "TCP endpoint unreachable");
        } catch { return Evidence(false, "TCP endpoint reachable; application health not checked", "TCP endpoint unreachable"); }
    }

    private EvidenceDto Evidence(bool healthy, string healthyDetail, string failedDetail) =>
        new(healthy ? FreshnessState.Fresh : FreshnessState.Stale, clock.GetUtcNow(), healthy ? healthyDetail : failedDetail);
}

using System.Security.Cryptography;
using System.Text;
using PosAdminTool.Domain.Interfaces;

namespace PosAdminTool.Agent.Services;

/// <summary>Resolves only the Agent configuration's service allowlist. Browser callers see an
/// opaque ID and can never provide a raw Windows service name to a control command.</summary>
public sealed class ServiceCatalog(IAgentConfigurationStore configurations)
{
    public async Task<IReadOnlyList<ConfiguredService>> ListAsync(CancellationToken cancellationToken)
    {
        var config = await configurations.LoadAsync(cancellationToken).ConfigureAwait(false);
        return config.Services
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new ConfiguredService(ToId(name), name))
            .OrderBy(service => service.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ConfiguredService?> FindAsync(string serviceId, CancellationToken cancellationToken) =>
        (await ListAsync(cancellationToken).ConfigureAwait(false)).SingleOrDefault(service => service.Id == serviceId);

    private static string ToId(string serviceName) => "svc-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serviceName.Trim()))).ToLowerInvariant()[..16];
}

public sealed record ConfiguredService(string Id, string DisplayName);

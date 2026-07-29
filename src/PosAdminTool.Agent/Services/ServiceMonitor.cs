using System.Collections.Concurrent;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Services;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Interfaces;

namespace PosAdminTool.Agent.Services;

/// <summary>Owns in-memory polled service state. A transition remains visible until the command
/// coordinator confirms its result, so the browser never mistakes command acceptance for success.</summary>
public sealed class ServiceMonitor(ServiceCatalog catalog, IServiceManager manager, TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, Snapshot> _snapshots = new(StringComparer.Ordinal);
    public event Action<IReadOnlyList<ServiceSummaryDto>>? Changed;

    public async Task<IReadOnlyList<ServiceSummaryDto>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var services = await catalog.ListAsync(cancellationToken).ConfigureAwait(false);
        var statuses = await manager.GetStatusesAsync(services.Select(service => service.DisplayName), cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var allowedIds = services.Select(service => service.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var stale in _snapshots.Keys.Where(id => !allowedIds.Contains(id))) _snapshots.TryRemove(stale, out _);
        foreach (var service in services)
        {
            if (_snapshots.TryGetValue(service.Id, out var prior) && prior.State == ServiceRuntimeState.Transitioning) continue;
            var status = statuses.GetValueOrDefault(service.DisplayName, ServiceStatus.Unknown);
            _snapshots[service.Id] = Snapshot.From(service, Map(status), now, Detail(status), prior?.Outcome);
        }

        var result = ToDtos(services);
        Changed?.Invoke(result);
        return result;
    }

    public async Task<ServiceSummaryDto?> GetAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        var all = await RefreshAsync(cancellationToken).ConfigureAwait(false);
        return all.SingleOrDefault(service => service.ServiceId == serviceId);
    }

    public async Task<ConfiguredService?> BeginAsync(string serviceId, ServiceActionKind action, CancellationToken cancellationToken)
    {
        var service = await catalog.FindAsync(serviceId, cancellationToken).ConfigureAwait(false);
        if (service is null) return null;
        var current = await GetAsync(serviceId, cancellationToken).ConfigureAwait(false);
        if (current is null || !current.AllowedActions.Contains(action)) return null;
        _snapshots[serviceId] = Snapshot.From(service, ServiceRuntimeState.Transitioning, clock.GetUtcNow(), $"{action} command sent; awaiting Agent confirmation", null);
        Changed?.Invoke(ToDtos(await catalog.ListAsync(cancellationToken).ConfigureAwait(false)));
        return service;
    }

    public async Task CompleteAsync(ConfiguredService service, ServiceActionKind action, bool confirmed, string outcome, CancellationToken cancellationToken)
    {
        var status = await manager.GetStatusAsync(service.DisplayName, cancellationToken).ConfigureAwait(false);
        var expected = action is ServiceActionKind.Start or ServiceActionKind.Restart ? ServiceRuntimeState.Running : ServiceRuntimeState.Stopped;
        var state = confirmed && Map(status) == expected ? expected : Map(status);
        _snapshots[service.Id] = Snapshot.From(service, state, clock.GetUtcNow(), Detail(status), confirmed && state == expected ? $"{action} confirmed" : outcome);
        Changed?.Invoke(ToDtos(await catalog.ListAsync(cancellationToken).ConfigureAwait(false)));
    }

    public async Task FailAsync(ConfiguredService service, string outcome, CancellationToken cancellationToken)
    {
        var status = await manager.GetStatusAsync(service.DisplayName, cancellationToken).ConfigureAwait(false);
        _snapshots[service.Id] = Snapshot.From(service, Map(status), clock.GetUtcNow(), Detail(status), outcome);
        Changed?.Invoke(ToDtos(await catalog.ListAsync(cancellationToken).ConfigureAwait(false)));
    }

    private IReadOnlyList<ServiceSummaryDto> ToDtos(IReadOnlyList<ConfiguredService> services) => services.Select(service =>
    {
        var snapshot = _snapshots.GetValueOrDefault(service.Id) ?? Snapshot.From(service, ServiceRuntimeState.Unknown, null, "Service has not been checked", null);
        return new ServiceSummaryDto(service.Id, service.DisplayName, snapshot.State, new EvidenceDto(snapshot.State is ServiceRuntimeState.Unknown or ServiceRuntimeState.NotFound ? FreshnessState.Stale : FreshnessState.Fresh, snapshot.CheckedAt, snapshot.Detail), Allowed(snapshot.State), snapshot.Outcome);
    }).ToList();

    private static IReadOnlyList<ServiceActionKind> Allowed(ServiceRuntimeState state) => state switch
    {
        ServiceRuntimeState.Running => [ServiceActionKind.Stop, ServiceActionKind.Restart],
        ServiceRuntimeState.Stopped => [ServiceActionKind.Start],
        _ => [],
    };
    private static ServiceRuntimeState Map(ServiceStatus status) => status switch { ServiceStatus.Running => ServiceRuntimeState.Running, ServiceStatus.Stopped => ServiceRuntimeState.Stopped, ServiceStatus.NotFound => ServiceRuntimeState.NotFound, _ => ServiceRuntimeState.Unknown };
    private static string Detail(ServiceStatus status) => status switch { ServiceStatus.Running => "Windows service is running", ServiceStatus.Stopped => "Windows service is stopped", ServiceStatus.NotFound => "Configured Windows service was not found", _ => "Windows service state is unavailable" };
    private sealed record Snapshot(ConfiguredService Service, ServiceRuntimeState State, DateTimeOffset? CheckedAt, string Detail, string? Outcome)
    { public static Snapshot From(ConfiguredService service, ServiceRuntimeState state, DateTimeOffset? checkedAt, string detail, string? outcome) => new(service, state, checkedAt, detail, outcome); }
}

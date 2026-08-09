using System.Collections.Concurrent;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Interfaces;

namespace PosAdminTool.Agent.IntegrationTests.TestSupport;

/// <summary>Deterministic service-control seam for Agent endpoint tests. It never contacts SCM.</summary>
public sealed class FakeServiceManager : IServiceManager
{
    private readonly ConcurrentDictionary<string, ServiceStatus> _statuses = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string Service, ServiceControlAction Action), Exception> _failures = new();

    public ConcurrentQueue<(string Service, ServiceControlAction Action)> Controls { get; } = new();

    public void SetStatus(string service, ServiceStatus status) => _statuses[service] = status;

    public void Fail(string service, ServiceControlAction action, Exception exception) => _failures[(service, action)] = exception;

    public void Clear()
    {
        _statuses.Clear();
        _failures.Clear();
        while (Controls.TryDequeue(out _)) { }
    }

    public Task<ServiceStatus> GetStatusAsync(string serviceName, CancellationToken cancellationToken = default) =>
        Task.FromResult(_statuses.GetValueOrDefault(serviceName, ServiceStatus.NotFound));

    public Task<IReadOnlyDictionary<string, ServiceStatus>> GetStatusesAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, ServiceStatus>>(serviceNames.ToDictionary(name => name, name => _statuses.GetValueOrDefault(name, ServiceStatus.NotFound), StringComparer.Ordinal));

    public Task ControlAsync(string serviceName, ServiceControlAction action, CancellationToken cancellationToken = default)
    {
        Controls.Enqueue((serviceName, action));
        if (_failures.TryGetValue((serviceName, action), out var failure)) return Task.FromException(failure);

        _statuses[serviceName] = action is ServiceControlAction.Stop ? ServiceStatus.Stopped : ServiceStatus.Running;
        return Task.CompletedTask;
    }
}

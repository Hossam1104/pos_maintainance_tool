using System.Collections.Concurrent;
using System.Threading.Channels;
using PosAdminTool.Agent.Audit;
using PosAdminTool.Contracts.V1.Services;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Domain.Interfaces;

namespace PosAdminTool.Agent.Services;

/// <summary>Queues bounded service commands. Calls are serialized per opaque service ID and are
/// idempotent per authenticated principal, while the monitor is the browser's source of truth.</summary>
public sealed class ServiceCommandCoordinator(ServiceCatalog catalog, ServiceMonitor monitor)
{
    private readonly Channel<ServiceCommand> _queue = Channel.CreateBounded<ServiceCommand>(new BoundedChannelOptions(32) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
    private readonly ConcurrentDictionary<string, ServiceCommand> _idempotency = new(StringComparer.Ordinal);

    public async Task<ServiceCommandSubmitResult> SubmitAsync(string serviceId, ServiceActionKind action, string principal, string correlationId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var idempotencyId = $"{principal}\n{idempotencyKey}";
        if (_idempotency.TryGetValue(idempotencyId, out var duplicate)) return new(ServiceCommandSubmitStatus.Duplicate, duplicate.ServiceId);
        var service = await catalog.FindAsync(serviceId, cancellationToken).ConfigureAwait(false);
        if (service is null) return new(ServiceCommandSubmitStatus.NotFound, null);
        if (await monitor.BeginAsync(serviceId, action, cancellationToken).ConfigureAwait(false) is null) return new(ServiceCommandSubmitStatus.Conflict, serviceId);
        var command = new ServiceCommand(service, action, principal, correlationId, idempotencyId);
        if (!_queue.Writer.TryWrite(command))
        {
            await monitor.FailAsync(service, "Command queue is full; no service action was started.", cancellationToken).ConfigureAwait(false);
            return new(ServiceCommandSubmitStatus.QueueFull, serviceId);
        }
        _idempotency.TryAdd(idempotencyId, command);
        return new(ServiceCommandSubmitStatus.Accepted, serviceId);
    }

    internal IAsyncEnumerable<ServiceCommand> ReadAllAsync(CancellationToken cancellationToken) => _queue.Reader.ReadAllAsync(cancellationToken);
}

public sealed class ServiceCommandWorker(ServiceCommandCoordinator commands, IServiceManager manager, ServiceMonitor monitor, OperationAuditWriter audit, ILogger<ServiceCommandWorker> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _serviceLocks = new(StringComparer.Ordinal);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // SCM actions cannot be safely reversed after acceptance. The API therefore
        // has no requester-cancel operation: commands have a bounded timeout, and
        // host shutdown is the only cancellation path.
        await foreach (var command in commands.ReadAllAsync(stoppingToken))
        {
            var gate = _serviceLocks.GetOrAdd(command.ServiceId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(stoppingToken).ConfigureAwait(false);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(35));
                await manager.ControlAsync(command.Service.DisplayName, ToDomain(command.Action), timeout.Token).ConfigureAwait(false);
                await monitor.CompleteAsync(command.Service, command.Action, true, "Command did not reach its expected state", stoppingToken).ConfigureAwait(false);
                await audit.AppendServiceActionAsync(command.ServiceId, command.Service.DisplayName, command.Action, command.Principal, command.CorrelationId, "confirmed", stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var outcome = ex switch { UnauthorizedAccessException => "Access denied", OperationCanceledException => "Command timed out or was cancelled", TimeoutException => "Command timed out or was cancelled", _ => "Command failed" };
                logger.LogWarning(ex, "Service command failed for {ServiceId}.", command.ServiceId);
                await monitor.FailAsync(command.Service, outcome, CancellationToken.None).ConfigureAwait(false);
                await audit.AppendServiceActionAsync(command.ServiceId, command.Service.DisplayName, command.Action, command.Principal, command.CorrelationId, outcome, CancellationToken.None).ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }
    }
    private static ServiceControlAction ToDomain(ServiceActionKind action) => action switch { ServiceActionKind.Start => ServiceControlAction.Start, ServiceActionKind.Stop => ServiceControlAction.Stop, ServiceActionKind.Restart => ServiceControlAction.Restart, _ => throw new ArgumentOutOfRangeException(nameof(action)) };
}

public sealed record ServiceCommand(ConfiguredService Service, ServiceActionKind Action, string Principal, string CorrelationId, string IdempotencyId)
{ public string ServiceId => Service.Id; }
public sealed record ServiceCommandSubmitResult(ServiceCommandSubmitStatus Status, string? ServiceId);
public enum ServiceCommandSubmitStatus { Accepted, Duplicate, NotFound, Conflict, QueueFull }

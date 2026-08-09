using System.Text.Json;
using PosAdminTool.Agent.Operations;
using PosAdminTool.Contracts.V1.Services;
using PosAdminTool.Infrastructure.Configuration;

namespace PosAdminTool.Agent.Audit;
public sealed class OperationAuditWriter(AgentConfigurationStoreOptions options)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task AppendAsync(OperationRegistry.Entry entry, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(options.RootDirectory, "audit"); ServiceOwnedDirectoryProvisioner.EnsureProvisioned(directory);
        var line = JsonSerializer.Serialize(new
        {
            operationId = entry.Id,
            operationType = entry.Type,
            branchCode = entry.Branch,
            principal = entry.Principal,
            requestedAtUtc = entry.Requested,
            completedAtUtc = entry.Ended,
            state = entry.State.ToString(),
            errorCode = entry.ErrorCode,
            operationMode = entry.OperationMode,
            operationTarget = entry.OperationTarget,
            resultArtifactIds = entry.ResultArtifactIds,
            maintenanceOutcome = entry.MaintenanceOutcome,
            correlationId = entry.Correlation,
        });
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await File.AppendAllTextAsync(Path.Combine(directory, "operations.jsonl"), line + Environment.NewLine, cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async Task AppendServiceActionAsync(string serviceId, string displayName, ServiceActionKind action, string principal, string correlationId, string outcome, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(options.RootDirectory, "audit"); ServiceOwnedDirectoryProvisioner.EnsureProvisioned(directory);
        var line = JsonSerializer.Serialize(new { category = "service", serviceId, displayName, action = action.ToString(), principal, correlationId, outcome, atUtc = DateTimeOffset.UtcNow });
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await File.AppendAllTextAsync(Path.Combine(directory, "operations.jsonl"), line + Environment.NewLine, cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
}

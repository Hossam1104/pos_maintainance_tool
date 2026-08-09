using System.Text.Json;
using PosAdminTool.Contracts.V1.Downloader;
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
            downloaderOutcome = ToAuditDownloaderOutcome(entry.DownloaderOutcome),
            correlationId = entry.Correlation,
        });
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await File.AppendAllTextAsync(Path.Combine(directory, "operations.jsonl"), line + Environment.NewLine, cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private static object? ToAuditDownloaderOutcome(DownloaderOperationOutcomeDto? outcome) =>
        outcome is null
            ? null
            : new
            {
                outcome.Branches,
                outcome.Serial,
                TriggerState = ToCamelCase(outcome.TriggerState),
                outcome.OperatorGuidance,
                outcome.TriggerAccepted,
            };

    private static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();
        return string.IsNullOrEmpty(name)
            ? string.Empty
            : char.ToLowerInvariant(name[0]) + name[1..];
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

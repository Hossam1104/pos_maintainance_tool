using System.Text.Json;
using PosAdminTool.Agent.Operations;
using PosAdminTool.Infrastructure.Configuration;

namespace PosAdminTool.Agent.Audit;
public sealed class OperationAuditWriter(AgentConfigurationStoreOptions options)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task AppendAsync(OperationRegistry.Entry entry, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(options.RootDirectory, "audit"); ServiceOwnedDirectoryProvisioner.EnsureProvisioned(directory);
        var line = JsonSerializer.Serialize(new { operationId = entry.Id, operationType = entry.Type, branchCode = entry.Branch, principal = entry.Principal, requestedAtUtc = entry.Requested, completedAtUtc = entry.Ended, state = entry.State.ToString(), correlationId = entry.Correlation });
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await File.AppendAllTextAsync(Path.Combine(directory, "operations.jsonl"), line + Environment.NewLine, cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
}

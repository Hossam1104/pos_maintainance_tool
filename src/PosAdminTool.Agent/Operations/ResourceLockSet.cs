using System.Collections.Concurrent;

namespace PosAdminTool.Agent.Operations;

/// <summary>Named locks serialize conflicting work in queue order; clients are never asked to retry a destructive command.</summary>
public sealed class ResourceLockSet
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal) { ["sql"] = new(1, 1), ["services"] = new(1, 1), ["filesystem-cleanup"] = new(1, 1), ["downloader"] = new(1, 1) };
    public async Task<IDisposable> AcquireAsync(IEnumerable<string> names, CancellationToken cancellationToken)
    {
        var acquired = new List<SemaphoreSlim>();
        try { foreach (var name in names.OrderBy(x => x, StringComparer.Ordinal)) { var gate = _locks[name]; await gate.WaitAsync(cancellationToken).ConfigureAwait(false); acquired.Add(gate); } return new Releaser(acquired); }
        catch { foreach (var gate in acquired) gate.Release(); throw; }
    }
    private sealed class Releaser(List<SemaphoreSlim> gates) : IDisposable { public void Dispose() { foreach (var gate in gates.AsEnumerable().Reverse()) gate.Release(); } }
}

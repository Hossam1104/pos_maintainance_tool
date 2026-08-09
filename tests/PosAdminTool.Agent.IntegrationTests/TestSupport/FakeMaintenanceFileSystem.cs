using System.Collections.Concurrent;
using PosAdminTool.Domain.Interfaces;

namespace PosAdminTool.Agent.IntegrationTests.TestSupport;

/// <summary>In-memory maintenance filesystem double. It never inspects or deletes a host path.</summary>
public sealed class FakeMaintenanceFileSystem : IMaintenanceFileSystem
{
    private readonly ConcurrentDictionary<string, MaintenancePathInspection> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IReadOnlyList<MaintenancePathInspection>> _ancestors = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Exception> _deleteFailures = new(StringComparer.OrdinalIgnoreCase);

    public ConcurrentQueue<string> DeleteCalls { get; } = new();

    public Exception? DeleteFailure { get; set; }

    public bool BlockDelete { get; set; }

    public TaskCompletionSource DeleteStarted { get; private set; } = NewSignal();

    public TaskCompletionSource DeleteRelease { get; private set; } = NewSignal();

    public void ResetSignals()
    {
        DeleteStarted = NewSignal();
        DeleteRelease = NewSignal();
    }

    public void Clear()
    {
        _entries.Clear();
        _ancestors.Clear();
        _deleteFailures.Clear();
        while (DeleteCalls.TryDequeue(out _)) { }
        DeleteFailure = null;
        BlockDelete = false;
        ResetSignals();
    }

    public void SetEntry(
        string path,
        bool exists = true,
        bool isDirectory = true,
        long? lengthBytes = null,
        int? childCount = 1)
    {
        var canonical = GetFullPath(path);
        _entries[canonical] = new(canonical, exists, isDirectory, false, null, lengthBytes, childCount);
    }

    public void SetReparseEntry(string path, string? resolvedTarget)
    {
        var canonical = GetFullPath(path);
        _entries[canonical] = new(canonical, true, true, true, resolvedTarget, null, 1);
    }

    public void SetAncestors(string targetPath, params MaintenancePathInspection[] inspections) =>
        _ancestors[GetFullPath(targetPath)] = inspections;

    public void FailDelete(string path, Exception failure) => _deleteFailures[GetFullPath(path)] = failure;

    public string ExpandEnvironmentVariables(string path) => Environment.ExpandEnvironmentVariables(path);

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public MaintenancePathInspection Inspect(string path) =>
        _entries.GetValueOrDefault(GetFullPath(path), new(GetFullPath(path), false, false, false, null, null, null));

    public IReadOnlyList<MaintenancePathInspection> InspectAncestors(string path)
    {
        var canonical = GetFullPath(path);
        return _ancestors.GetValueOrDefault(canonical, [Inspect(canonical)]);
    }

    public long? TryGetAvailableFreeSpace(string path) => 10L * 1024 * 1024;

    public async Task DeleteAsync(string path, bool recursive, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Enqueue(GetFullPath(path));
        DeleteStarted.TrySetResult();
        if (BlockDelete) await DeleteRelease.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (_deleteFailures.TryGetValue(GetFullPath(path), out var targetFailure)) throw targetFailure;
        if (DeleteFailure is not null) throw DeleteFailure;
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Files;

namespace PosAdminTool.Agent.Files;

/// <summary>
/// Bounded in-memory handle registry. A handle is a capability to redeem ONE specific
/// (root, sub-path) pair for its declared purpose, once, before it expires — never a durable
/// reference to arbitrary bytes (plan section 5.7). Wrong-principal and wrong-purpose attempts
/// do not consume the handle, so the legitimate holder can still redeem it once.
/// </summary>
public sealed class InMemoryFileHandleStore
    : IFileHandleStore
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly RuntimeRetentionPolicy _retention;
    private readonly Dictionary<string, Entry> _handles = new(StringComparer.Ordinal);

    public InMemoryFileHandleStore(TimeProvider timeProvider)
        : this(timeProvider, RuntimeRetentionPolicy.Default)
    {
    }

    public InMemoryFileHandleStore(TimeProvider timeProvider, RuntimeRetentionPolicy retention)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _retention = retention ?? throw new ArgumentNullException(nameof(retention));
        _retention.Validate();
    }

    public int Count
    {
        get { lock (_gate) return _handles.Count; }
    }

    public FileHandleDto Issue(string principalName, string rootId, string relativeSubPath, FileHandlePurpose purpose)
    {
        var handleId = Guid.NewGuid().ToString("N");
        var expiresAtUtc = _timeProvider.GetUtcNow().Add(_retention.FileHandleLifetime);
        lock (_gate)
        {
            PruneExpiredLocked(_timeProvider.GetUtcNow());
            if (_handles.Count >= _retention.MaxFileHandles)
            {
                throw new FileHandleStoreCapacityException();
            }

            _handles[handleId] = new Entry(principalName, rootId, relativeSubPath, purpose, expiresAtUtc);
        }

        return new FileHandleDto(handleId, purpose, expiresAtUtc);
    }

    public FileHandleRedemption Redeem(string handleId, string principalName, FileHandlePurpose expectedPurpose)
    {
        lock (_gate)
        {
            PruneExpiredLocked(_timeProvider.GetUtcNow(), handleId);
            if (!_handles.TryGetValue(handleId, out var entry))
            {
                return Fail(ErrorCodes.HandleNotFound);
            }

            if (_timeProvider.GetUtcNow() >= entry.ExpiresAtUtc)
            {
                _handles.Remove(handleId);
                return Fail(ErrorCodes.HandleExpired);
            }

            if (!string.Equals(entry.PrincipalName, principalName, StringComparison.Ordinal))
            {
                return Fail(ErrorCodes.HandleWrongPrincipal);
            }

            if (entry.Purpose != expectedPurpose)
            {
                return Fail(ErrorCodes.HandleWrongPurpose);
            }

            if (!entry.TryMarkUsed())
            {
                return Fail(ErrorCodes.HandleAlreadyUsed);
            }

            return new FileHandleRedemption(true, entry.RootId, entry.RelativeSubPath, null);
        }
    }

    private void PruneExpiredLocked(DateTimeOffset now, string? preserveHandleId = null)
    {
        foreach (var pair in _handles.ToArray())
        {
            if (string.Equals(pair.Key, preserveHandleId, StringComparison.Ordinal)) continue;
            if (now >= pair.Value.ExpiresAtUtc) _handles.Remove(pair.Key);
        }
    }

    private static FileHandleRedemption Fail(string errorCode) => new(false, null, null, errorCode);

    private sealed class Entry(
        string principalName,
        string rootId,
        string relativeSubPath,
        FileHandlePurpose purpose,
        DateTimeOffset expiresAtUtc)
    {
        private int _used;

        public string PrincipalName { get; } = principalName;

        public string RootId { get; } = rootId;

        public string RelativeSubPath { get; } = relativeSubPath;

        public FileHandlePurpose Purpose { get; } = purpose;

        public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;

        public bool TryMarkUsed() => Interlocked.Exchange(ref _used, 1) == 0;
    }
}

public sealed class FileHandleStoreCapacityException : InvalidOperationException
{
    public FileHandleStoreCapacityException()
        : base("The file-handle retention limit has been reached.")
    {
    }
}

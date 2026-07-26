using System.Collections.Concurrent;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Files;

namespace PosAdminTool.Agent.Files;

/// <summary>
/// Bounded in-memory handle registry. A handle is a capability to redeem ONE specific
/// (root, sub-path) pair for its declared purpose, once, before it expires — never a durable
/// reference to arbitrary bytes (plan section 5.7). Wrong-principal and wrong-purpose attempts do
/// not consume the handle, so the legitimate holder can still redeem it once.
/// </summary>
public sealed class InMemoryFileHandleStore(TimeProvider timeProvider) : IFileHandleStore
{
    private static readonly TimeSpan HandleLifetime = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, Entry> _handles = new(StringComparer.Ordinal);

    public FileHandleDto Issue(string principalName, string rootId, string relativeSubPath, FileHandlePurpose purpose)
    {
        var handleId = Guid.NewGuid().ToString("N");
        var expiresAtUtc = timeProvider.GetUtcNow().Add(HandleLifetime);

        _handles[handleId] = new Entry(principalName, rootId, relativeSubPath, purpose, expiresAtUtc);

        return new FileHandleDto(handleId, purpose, expiresAtUtc);
    }

    public FileHandleRedemption Redeem(string handleId, string principalName, FileHandlePurpose expectedPurpose)
    {
        if (!_handles.TryGetValue(handleId, out var entry))
        {
            return Fail(ErrorCodes.HandleNotFound);
        }

        if (timeProvider.GetUtcNow() > entry.ExpiresAtUtc)
        {
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

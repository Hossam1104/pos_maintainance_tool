using System.Security.Cryptography;
using System.Text;
using PosAdminTool.Agent;
using PosAdminTool.Application.Maintenance;
using PosAdminTool.Contracts.V1.Common;

namespace PosAdminTool.Agent.Maintenance;

/// <summary>Bounded, principal-scoped, fresh one-use challenge store for maintenance actions.</summary>
public sealed class MaintenanceChallengeStore(
    TimeProvider timeProvider,
    RuntimeRetentionPolicy retention)
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly RuntimeRetentionPolicy _retention = retention ?? throw new ArgumentNullException(nameof(retention));
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public int Count
    {
        get { lock (_gate) return _entries.Count; }
    }

    public MaintenanceChallenge Issue(string principal, MaintenancePreviewIntent intent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
        ArgumentNullException.ThrowIfNull(intent);
        lock (_gate)
        {
            PruneLocked(_timeProvider.GetUtcNow());
            if (_entries.Count >= _retention.MaxMaintenanceChallenges)
            {
                throw new MaintenanceChallengeCapacityException();
            }

            var id = Guid.NewGuid().ToString("N");
            var expires = _timeProvider.GetUtcNow().Add(_retention.MaintenanceChallengeLifetime);
            _entries[id] = new Entry(id, principal, intent, expires);
            return new MaintenanceChallenge(id, expires);
        }
    }

    public bool TryGetIntent(
        string challengeId,
        string principal,
        out MaintenancePreviewIntent? intent,
        out string? errorCode)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(challengeId) || !_entries.TryGetValue(challengeId, out var entry))
            {
                intent = null;
                errorCode = ErrorCodes.MaintenanceChallengeNotFound;
                return false;
            }

            if (!string.Equals(entry.Principal, principal, StringComparison.Ordinal))
            {
                intent = null;
                errorCode = ErrorCodes.MaintenanceChallengeWrongPrincipal;
                return false;
            }

            if (entry.Used)
            {
                intent = null;
                errorCode = ErrorCodes.MaintenanceChallengeUsed;
                return false;
            }

            if (_timeProvider.GetUtcNow() >= entry.ExpiresAtUtc)
            {
                entry.Used = true;
                intent = null;
                errorCode = ErrorCodes.MaintenanceChallengeExpired;
                return false;
            }

            intent = entry.Intent;
            errorCode = null;
            return true;
        }
    }

    public MaintenanceChallengeRedemption Redeem(
        string challengeId,
        string principal,
        string expectedFingerprint,
        string typedConfirmation)
    {
        lock (_gate)
        {
            if (!TryFind(challengeId, out var entry)) return Fail(ErrorCodes.MaintenanceChallengeNotFound);
            if (!string.Equals(entry.Principal, principal, StringComparison.Ordinal))
                return Fail(ErrorCodes.MaintenanceChallengeWrongPrincipal);
            if (entry.Used) return Fail(ErrorCodes.MaintenanceChallengeUsed);
            if (_timeProvider.GetUtcNow() >= entry.ExpiresAtUtc)
            {
                entry.Used = true;
                return Fail(ErrorCodes.MaintenanceChallengeExpired);
            }

            if (!FixedTimeEquals(entry.Intent.Fingerprint, expectedFingerprint))
            {
                entry.Used = true;
                return Fail(ErrorCodes.MaintenanceChallengeChanged);
            }

            if (!FixedTimeEquals(entry.Intent.ConfirmationText, typedConfirmation))
            {
                entry.Used = true;
                return Fail(ErrorCodes.MaintenanceConfirmationMismatch);
            }

            entry.Used = true;
            return new(true, entry.Intent, null);
        }
    }

    public bool Invalidate(string challengeId, string principal)
    {
        lock (_gate)
        {
            if (!TryFind(challengeId, out var entry)
                || !string.Equals(entry.Principal, principal, StringComparison.Ordinal)
                || entry.Used)
            {
                return false;
            }

            entry.Used = true;
            return true;
        }
    }

    public int Prune()
    {
        lock (_gate) return PruneLocked(_timeProvider.GetUtcNow());
    }

    private bool TryFind(string challengeId, out Entry entry)
    {
        if (string.IsNullOrWhiteSpace(challengeId))
        {
            entry = null!;
            return false;
        }

        return _entries.TryGetValue(challengeId, out entry!);
    }

    private int PruneLocked(DateTimeOffset now)
    {
        var expired = _entries.Values.Where(entry => now >= entry.ExpiresAtUtc).ToList();
        foreach (var entry in expired) _entries.Remove(entry.ChallengeId);
        return expired.Count;
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected ?? string.Empty);
        var actualBytes = Encoding.UTF8.GetBytes(actual ?? string.Empty);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static MaintenanceChallengeRedemption Fail(string errorCode) => new(false, null, errorCode);

    private sealed class Entry(
        string challengeId,
        string principal,
        MaintenancePreviewIntent intent,
        DateTimeOffset expiresAtUtc)
    {
        public string ChallengeId { get; } = challengeId;
        public string Principal { get; } = principal;
        public MaintenancePreviewIntent Intent { get; } = intent;
        public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;
        public bool Used { get; set; }
    }
}

public sealed record MaintenanceChallenge(string ChallengeId, DateTimeOffset ExpiresAtUtc);

public sealed record MaintenanceChallengeRedemption(
    bool Success,
    MaintenancePreviewIntent? Intent,
    string? ErrorCode);

public sealed class MaintenanceChallengeCapacityException()
    : InvalidOperationException("The maintenance challenge retention limit has been reached.");

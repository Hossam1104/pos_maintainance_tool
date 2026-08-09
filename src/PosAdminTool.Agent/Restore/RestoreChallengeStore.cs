using System.Security.Cryptography;
using System.Text;
using PosAdminTool.Application.Restore;
using PosAdminTool.Contracts.V1.Common;

namespace PosAdminTool.Agent.Restore;

/// <summary>
/// Bounded, short-lived, one-use challenge store. Each entry is bound to the authenticated
/// principal and the complete immutable server-side intent fingerprint; client preview DTOs never
/// populate or mutate the stored intent.
/// </summary>
public sealed class RestoreChallengeStore
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly RuntimeRetentionPolicy _retention;
    private readonly RestoreUploadStore? _uploads;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public RestoreChallengeStore(
        TimeProvider timeProvider,
        RuntimeRetentionPolicy retention,
        RestoreUploadStore? uploads = null)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _retention = retention ?? throw new ArgumentNullException(nameof(retention));
        _retention.Validate();
        _uploads = uploads;
    }

    public int Count
    {
        get { lock (_gate) return _entries.Count; }
    }

    public RestoreChallenge Issue(string principal, RestorePreviewIntent intent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
        ArgumentNullException.ThrowIfNull(intent);
        lock (_gate)
        {
            PruneLocked(_timeProvider.GetUtcNow());
            if (_entries.Count >= _retention.MaxRestoreChallenges)
            {
                throw new RestoreChallengeCapacityException();
            }

            var challengeId = Guid.NewGuid().ToString("N");
            var expiresAtUtc = _timeProvider.GetUtcNow().Add(_retention.RestoreChallengeLifetime);
            _entries[challengeId] = new Entry(challengeId, principal, intent, expiresAtUtc);
            return new RestoreChallenge(challengeId, expiresAtUtc);
        }
    }

    public RestoreChallengeRedemption Redeem(
        string challengeId,
        string principal,
        string expectedFingerprint,
        string typedConfirmation)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(challengeId) || !_entries.TryGetValue(challengeId, out var entry))
                return Fail(ErrorCodes.RestoreChallengeNotFound);
            if (!string.Equals(entry.Principal, principal, StringComparison.Ordinal))
                return Fail(ErrorCodes.RestoreChallengeWrongPrincipal);
            if (entry.Used)
                return Fail(ErrorCodes.RestoreChallengeUsed);
            if (_timeProvider.GetUtcNow() >= entry.ExpiresAtUtc)
            {
                entry.Used = true;
                ReleaseUpload(entry.Intent);
                return Fail(ErrorCodes.RestoreChallengeExpired);
            }

            if (!FixedTimeEquals(entry.Intent.Fingerprint, expectedFingerprint))
            {
                entry.Used = true;
                ReleaseUpload(entry.Intent);
                return Fail(ErrorCodes.RestoreChallengeChanged);
            }

            if (!FixedTimeEquals(entry.Intent.ConfirmationText, typedConfirmation))
            {
                entry.Used = true;
                ReleaseUpload(entry.Intent);
                return Fail(ErrorCodes.RestoreConfirmationMismatch);
            }

            entry.Used = true;
            return new RestoreChallengeRedemption(true, entry.Intent, null);
        }
    }

    /// <summary>Read-only server lookup used only to recompute policy before one-use redemption.</summary>
    public bool TryGetIntent(string challengeId, string principal, out RestorePreviewIntent? intent, out string? errorCode)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(challengeId) || !_entries.TryGetValue(challengeId, out var entry))
            {
                intent = null;
                errorCode = ErrorCodes.RestoreChallengeNotFound;
                return false;
            }

            if (!string.Equals(entry.Principal, principal, StringComparison.Ordinal))
            {
                intent = null;
                errorCode = ErrorCodes.RestoreChallengeWrongPrincipal;
                return false;
            }

            if (entry.Used)
            {
                intent = null;
                errorCode = ErrorCodes.RestoreChallengeUsed;
                return false;
            }

            if (_timeProvider.GetUtcNow() >= entry.ExpiresAtUtc)
            {
                entry.Used = true;
                ReleaseUpload(entry.Intent);
                intent = null;
                errorCode = ErrorCodes.RestoreChallengeExpired;
                return false;
            }

            intent = entry.Intent;
            errorCode = null;
            return true;
        }
    }

    /// <summary>Invalidates a challenge after execute-time source/policy recomputation fails.</summary>
    public bool Invalidate(string challengeId, string principal, string? errorCode = null)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(challengeId, out var entry)
                || !string.Equals(entry.Principal, principal, StringComparison.Ordinal)
                || entry.Used)
            {
                return false;
            }

            entry.Used = true;
            ReleaseUpload(entry.Intent);
            return true;
        }
    }

    public int Prune()
    {
        lock (_gate) return PruneLocked(_timeProvider.GetUtcNow());
    }

    private int PruneLocked(DateTimeOffset now)
    {
        var expired = _entries.Values.Where(entry => now >= entry.ExpiresAtUtc).ToList();
        foreach (var entry in expired)
        {
            _entries.Remove(entry.ChallengeId);
            if (!entry.Used) ReleaseUpload(entry.Intent);
        }

        return expired.Count;
    }

    private void ReleaseUpload(RestorePreviewIntent intent) => _uploads?.Release(intent.Source.UploadId);

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected ?? string.Empty);
        var actualBytes = Encoding.UTF8.GetBytes(actual ?? string.Empty);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static RestoreChallengeRedemption Fail(string code) => new(false, null, code);

    private sealed class Entry(
        string challengeId,
        string principal,
        RestorePreviewIntent intent,
        DateTimeOffset expiresAtUtc)
    {
        public string ChallengeId { get; } = challengeId;
        public string Principal { get; } = principal;
        public RestorePreviewIntent Intent { get; } = intent;
        public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;
        public bool Used { get; set; }
    }
}

public sealed record RestoreChallenge(string ChallengeId, DateTimeOffset ExpiresAtUtc);

public sealed record RestoreChallengeRedemption(
    bool Success,
    RestorePreviewIntent? Intent,
    string? ErrorCode);

public sealed class RestoreChallengeCapacityException()
    : InvalidOperationException("The restore challenge retention limit has been reached.");

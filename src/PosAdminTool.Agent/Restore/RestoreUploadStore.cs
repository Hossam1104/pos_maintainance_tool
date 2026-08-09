using PosAdminTool.Application.Restore;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Restore;
using PosAdminTool.Domain.Interfaces;

namespace PosAdminTool.Agent.Restore;

/// <summary>
/// Bounded streamed upload staging. Files are admitted only after the byte limit, total staged-byte
/// limit, and retention capacity have been enforced. Every rejection, cancellation, failed copy,
/// expiry, and disposal path removes the temporary file.
/// </summary>
public sealed class RestoreUploadStore : IDisposable
{
    private readonly object _gate = new();
    private readonly IRestoreFileSystem _fileSystem;
    private readonly TimeProvider _timeProvider;
    private readonly RuntimeRetentionPolicy _retention;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly string _rootDirectory;
    private long _reservedBytes;
    private int _inFlightUploads;
    private bool _disposed;

    public RestoreUploadStore(
        IRestoreFileSystem fileSystem,
        TimeProvider timeProvider,
        RuntimeRetentionPolicy retention,
        string? rootDirectory = null)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _retention = retention ?? throw new ArgumentNullException(nameof(retention));
        _retention.Validate();
        _rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(Path.GetTempPath(), "PosAdminTool", "restore-uploads")
            : _fileSystem.GetFullPath(rootDirectory);
    }

    public int Count
    {
        get { lock (_gate) return _entries.Count; }
    }

    public long StagedBytes
    {
        get { lock (_gate) return checked(_entries.Values.Sum(entry => entry.SizeBytes) + _reservedBytes); }
    }

    public async Task<RestoreUploadDto> StageAsync(
        string principal,
        string requestedFileName,
        Stream input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
        ArgumentNullException.ThrowIfNull(input);
        var fileName = ValidateFileName(requestedFileName);
        Prune();

        lock (_gate)
        {
            ThrowIfDisposedLocked();
            if (_entries.Count + _inFlightUploads >= _retention.MaxRestoreUploads)
            {
                throw new RestoreUploadCapacityException();
            }

            // Reserve an upload slot before any filesystem work. Bytes are reserved as they are
            // read below, so concurrent writers cannot collectively exceed the staged-byte cap.
            _inFlightUploads++;
        }

        var uploadId = Guid.NewGuid().ToString("N");
        var path = Path.Combine(_rootDirectory, $"{uploadId}.upload");
        long size = 0;
        long reservedBytes = 0;
        var reservationActive = true;
        try
        {
            EnsureSafeRoot();

            await _fileSystem.EnsureDirectoryAsync(_rootDirectory, cancellationToken).ConfigureAwait(false);
            EnsureSafeRoot();

            await using (var output = await _fileSystem.CreateFileAsync(path, cancellationToken).ConfigureAwait(false))
            {
                var buffer = new byte[128 * 1024];
                while (true)
                {
                    var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                    if (read == 0) break;
                    size = checked(size + read);
                    if (size > _retention.MaxRestoreUploadBytes)
                    {
                        throw new RestoreUploadTooLargeException();
                    }

                    lock (_gate)
                    {
                        ThrowIfDisposedLocked();
                        if (checked(_entries.Values.Sum(entry => entry.SizeBytes) + _reservedBytes + read) > _retention.MaxRestoreStagedBytes)
                        {
                            throw new RestoreUploadCapacityException();
                        }

                        _reservedBytes = checked(_reservedBytes + read);
                        reservedBytes = checked(reservedBytes + read);
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }

                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            lock (_gate)
            {
                ThrowIfDisposedLocked();
                PruneLocked(_timeProvider.GetUtcNow(), out var expired);
                DeleteEntries(expired);
                if (_entries.Count >= _retention.MaxRestoreUploads
                    || _entries.Values.Sum(entry => entry.SizeBytes) > _retention.MaxRestoreStagedBytes - size)
                {
                    throw new RestoreUploadCapacityException();
                }

                var expiresAtUtc = _timeProvider.GetUtcNow().Add(_retention.RestoreUploadLifetime);
                _reservedBytes -= reservedBytes;
                _inFlightUploads--;
                reservationActive = false;
                _entries[uploadId] = new Entry(uploadId, principal, fileName, path, size, expiresAtUtc);
                return new RestoreUploadDto(uploadId, fileName, size, expiresAtUtc);
            }
        }
        catch
        {
            if (reservationActive)
            {
                lock (_gate)
                {
                    _reservedBytes -= reservedBytes;
                    _inFlightUploads = Math.Max(0, _inFlightUploads - 1);
                }
            }

            try { await _fileSystem.DeleteFileAsync(path, CancellationToken.None).ConfigureAwait(false); } catch { }
            throw;
        }
    }

    public RestoreUploadClaimResult Claim(string uploadId, string principal)
    {
        lock (_gate)
        {
            if (_disposed || string.IsNullOrWhiteSpace(uploadId) || !_entries.TryGetValue(uploadId, out var entry))
                return Fail(ErrorCodes.RestoreUploadNotFound);
            if (_timeProvider.GetUtcNow() >= entry.ExpiresAtUtc)
            {
                _entries.Remove(uploadId);
                DeleteEntry(entry);
                return Fail(ErrorCodes.RestoreUploadExpired);
            }

            PruneLocked(_timeProvider.GetUtcNow(), out var expired);
            DeleteEntries(expired);
            if (!_entries.TryGetValue(uploadId, out entry)) return Fail(ErrorCodes.RestoreUploadNotFound);
            if (!string.Equals(entry.Principal, principal, StringComparison.Ordinal))
                return Fail(ErrorCodes.RestoreUploadNotFound);
            if (entry.Claimed)
                return Fail(ErrorCodes.RestoreUploadAlreadyClaimed);

            entry.Claimed = true;
            return new RestoreUploadClaimResult(
                true,
                new RestoreUploadDescriptor(entry.UploadId, entry.FileName, entry.Path),
                null);
        }
    }

    public bool TryGetClaimed(string uploadId, string principal, out RestoreUploadDescriptor? descriptor)
    {
        Prune();
        lock (_gate)
        {
            if (_entries.TryGetValue(uploadId, out var entry)
                && entry.Claimed
                && _timeProvider.GetUtcNow() < entry.ExpiresAtUtc
                && string.Equals(entry.Principal, principal, StringComparison.Ordinal))
            {
                descriptor = new RestoreUploadDescriptor(entry.UploadId, entry.FileName, entry.Path);
                return true;
            }
        }

        descriptor = null;
        return false;
    }

    public void Release(string? uploadId)
    {
        if (string.IsNullOrWhiteSpace(uploadId)) return;
        Entry? entry = null;
        lock (_gate)
        {
            if (_entries.Remove(uploadId, out var removed)) entry = removed;
        }

        if (entry is not null) DeleteEntry(entry);
    }

    public int Prune()
    {
        List<Entry> expired;
        lock (_gate)
        {
            PruneLocked(_timeProvider.GetUtcNow(), out expired);
        }

        DeleteEntries(expired);
        return expired.Count;
    }

    public void Dispose()
    {
        List<Entry> entries;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            entries = [.. _entries.Values];
            _entries.Clear();
        }

        DeleteEntries(entries);
        GC.SuppressFinalize(this);
    }

    private void PruneLocked(DateTimeOffset now, out List<Entry> expired)
    {
        expired = _entries.Values.Where(entry => now >= entry.ExpiresAtUtc).ToList();
        foreach (var entry in expired) _entries.Remove(entry.UploadId);
    }

    private void DeleteEntries(IEnumerable<Entry> entries)
    {
        foreach (var entry in entries) DeleteEntry(entry);
    }

    private void DeleteEntry(Entry entry)
    {
        try { _fileSystem.DeleteFileAsync(entry.Path, CancellationToken.None).GetAwaiter().GetResult(); } catch { }
    }

    private void EnsureSafeRoot()
    {
        string current;
        try { current = _fileSystem.GetFullPath(_rootDirectory); }
        catch { throw new RestoreUploadInvalidException(); }

        while (!string.IsNullOrWhiteSpace(current))
        {
            if (_fileSystem.IsReparsePoint(current)) throw new RestoreUploadInvalidException();
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) break;
            current = parent;
        }
    }

    private void ThrowIfDisposedLocked()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(RestoreUploadStore));
    }

    private static RestoreUploadClaimResult Fail(string code) => new(false, null, code);

    private static string ValidateFileName(string requestedFileName)
    {
        var supplied = (requestedFileName ?? string.Empty).Trim();
        if (supplied.Length == 0 || supplied.Length > 128 || supplied.Contains('%') || supplied.Contains('\0')
            || supplied.Contains('/') || supplied.Contains('\\') || supplied.Contains(':'))
        {
            throw new RestoreUploadInvalidException();
        }

        var fileName = Path.GetFileName(supplied);
        if (!string.Equals(fileName, supplied, StringComparison.Ordinal))
        {
            throw new RestoreUploadInvalidException();
        }

        var extension = Path.GetExtension(fileName);
        if (!extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".bak", StringComparison.OrdinalIgnoreCase))
        {
            throw new RestoreUploadInvalidException();
        }

        return fileName;
    }

    private sealed class Entry(
        string uploadId,
        string principal,
        string fileName,
        string path,
        long sizeBytes,
        DateTimeOffset expiresAtUtc)
    {
        public string UploadId { get; } = uploadId;
        public string Principal { get; } = principal;
        public string FileName { get; } = fileName;
        public string Path { get; } = path;
        public long SizeBytes { get; } = sizeBytes;
        public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;
        public bool Claimed { get; set; }
    }
}

public sealed record RestoreUploadDescriptor(string UploadId, string FileName, string Path);

public sealed record RestoreUploadClaimResult(
    bool Success,
    RestoreUploadDescriptor? Descriptor,
    string? ErrorCode);

public sealed class RestoreUploadInvalidException()
    : InvalidOperationException("The restore upload metadata is invalid.");

public sealed class RestoreUploadTooLargeException()
    : InvalidOperationException("The restore upload exceeds its bounded size limit.");

public sealed class RestoreUploadCapacityException()
    : InvalidOperationException("The restore upload retention limit has been reached.");

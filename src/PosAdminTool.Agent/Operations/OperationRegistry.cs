using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using PosAdminTool.Contracts.V1.Activity;
using PosAdminTool.Contracts.V1.Operations;

namespace PosAdminTool.Agent.Operations;

public sealed class OperationRegistry
{
    private const int Capacity = 32;
    private readonly object _gate = new();
    private readonly RuntimeRetentionPolicy _retention;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _idempotency = new(StringComparer.Ordinal);
    private readonly Channel<Entry> _queue = Channel.CreateBounded<Entry>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
    });

    public OperationRegistry()
        : this(RuntimeRetentionPolicy.Default, TimeProvider.System)
    {
    }

    public OperationRegistry(TimeProvider timeProvider)
        : this(RuntimeRetentionPolicy.Default, timeProvider)
    {
    }

    public OperationRegistry(RuntimeRetentionPolicy retention, TimeProvider timeProvider)
    {
        _retention = retention ?? throw new ArgumentNullException(nameof(retention));
        _retention.Validate();
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public event Action<OperationDetailDto>? Changed;

    public bool TryGetIdempotent(string principal, string? idempotencyKey, out OperationDetailDto? detail)
    {
        detail = null;
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return false;

        lock (_gate)
        {
            PruneLocked(_timeProvider.GetUtcNow());
            var idempotencyId = BuildIdempotencyId(principal, idempotencyKey);
            if (!_idempotency.TryGetValue(idempotencyId, out var existingId)) return false;

            if (_entries.TryGetValue(existingId, out var existing))
            {
                detail = existing.ToDto();
                return true;
            }

            // A stale mapping is never allowed to block a safe retry after the operation record
            // has been evicted.
            _idempotency.TryRemove(new KeyValuePair<string, string>(idempotencyId, existingId));
            return false;
        }
    }

    public bool TrySubmit(
        string operationType,
        string branchCode,
        string principal,
        string correlationId,
        string? idempotencyKey,
        out OperationDetailDto? detail,
        out bool duplicate) =>
        TrySubmit(operationType, branchCode, principal, correlationId, idempotencyKey, null, null, out detail, out duplicate);

    public bool TrySubmit(
        string operationType,
        string branchCode,
        string principal,
        string correlationId,
        string? idempotencyKey,
        object? workItem,
        string? destinationReference,
        out OperationDetailDto? detail,
        out bool duplicate)
    {
        detail = null;
        duplicate = false;
        Entry? submitted = null;

        lock (_gate)
        {
            PruneLocked(_timeProvider.GetUtcNow());
            var idempotencyId = string.IsNullOrWhiteSpace(idempotencyKey)
                ? null
                : BuildIdempotencyId(principal, idempotencyKey);

            if (idempotencyId is not null && _idempotency.TryGetValue(idempotencyId, out var existingId))
            {
                if (_entries.TryGetValue(existingId, out var existing))
                {
                    duplicate = true;
                    detail = existing.ToDto();
                    return true;
                }

                _idempotency.TryRemove(new KeyValuePair<string, string>(idempotencyId, existingId));
            }

            var entry = new Entry(
                operationType,
                branchCode,
                principal,
                correlationId,
                workItem,
                destinationReference,
                _timeProvider,
                _retention);
            _entries[entry.Id] = entry;

            if (idempotencyId is not null && !_idempotency.TryAdd(idempotencyId, entry.Id))
            {
                _entries.TryRemove(new KeyValuePair<string, Entry>(entry.Id, entry));
                entry.Dispose();
                return false;
            }

            if (!_queue.Writer.TryWrite(entry))
            {
                _entries.TryRemove(new KeyValuePair<string, Entry>(entry.Id, entry));
                if (idempotencyId is not null)
                {
                    _idempotency.TryRemove(new KeyValuePair<string, string>(idempotencyId, entry.Id));
                }

                entry.Dispose();
                return false;
            }

            detail = entry.ToDto();
            submitted = entry;
        }

        Publish(submitted);
        return true;
    }

    /// <summary>Prunes expired/over-count completed state and returns the number of records removed.</summary>
    public int Prune()
    {
        lock (_gate) return PruneLocked(_timeProvider.GetUtcNow());
    }

    public IReadOnlyList<OperationSummaryDto> List()
    {
        lock (_gate)
        {
            PruneLocked(_timeProvider.GetUtcNow());
            return _entries.Values
                .Select(entry => entry.ToSummary())
                .OrderByDescending(entry => entry.RequestedAtUtc)
                .ToList();
        }
    }

    public IReadOnlyList<ActivityRecordDto> ListActivity()
    {
        lock (_gate)
        {
            PruneLocked(_timeProvider.GetUtcNow());
            var active = _entries.Values
                .Where(entry => entry.IsActive)
                .ToList();
            var completedSlots = Math.Max(0, _retention.MaxActivityEntries - active.Count);
            var recentCompleted = _entries.Values
                .Where(entry => !entry.IsActive)
                .OrderByDescending(entry => entry.CompletedAtUtc)
                .ThenByDescending(entry => entry.Id, StringComparer.Ordinal)
                .Take(completedSlots)
                .ToList();

            return active
                .Concat(recentCompleted)
                .Select(entry => entry.ToActivity())
                .OrderByDescending(entry => entry.AtUtc)
                .ThenByDescending(entry => entry.ActivityId, StringComparer.Ordinal)
                .ToList();
        }
    }

    public bool TryGet(string id, out OperationDetailDto? detail)
    {
        lock (_gate)
        {
            PruneLocked(_timeProvider.GetUtcNow());
            if (_entries.TryGetValue(id, out var entry))
            {
                detail = entry.ToDto();
                return true;
            }
        }

        detail = null;
        return false;
    }

    public bool Cancel(string id, out OperationDetailDto? detail)
    {
        Entry? entry;
        lock (_gate)
        {
            PruneLocked(_timeProvider.GetUtcNow());
            if (!_entries.TryGetValue(id, out entry))
            {
                detail = null;
                return false;
            }
        }

        entry.Cancel();
        detail = entry.ToDto();
        Publish(entry);
        return true;
    }

    /// <summary>
    /// Marks queued and running work as cancelled during worker shutdown. The worker audits and
    /// publishes the returned entries after all operation-local cancellation has been requested.
    /// </summary>
    public IReadOnlyList<Entry> CancelActive()
    {
        List<Entry> active;
        lock (_gate)
        {
            active = _entries.Values.Where(entry => entry.IsActive).ToList();
        }

        foreach (var entry in active)
        {
            entry.Cancel();
            if (entry.State == OperationState.Running)
            {
                entry.Complete(OperationState.Cancelled);
            }
        }

        return active;
    }

    public IAsyncEnumerable<Entry> ReadAllAsync(CancellationToken token) => _queue.Reader.ReadAllAsync(token);

    public void Publish(Entry entry)
    {
        Prune();
        Changed?.Invoke(entry.ToDto());
    }

    private int PruneLocked(DateTimeOffset now)
    {
        var completed = _entries.Values
            .Where(entry => !entry.IsActive)
            .Select(entry => new CompletedEntry(entry, entry.CompletedAtUtc))
            .OrderBy(entry => entry.EndedAtUtc)
            .ThenBy(entry => entry.Entry.Id, StringComparer.Ordinal)
            .ToList();
        var cutoff = now - _retention.CompletedOperationLifetime;
        var retainedIds = completed
            .Where(item => item.EndedAtUtc > cutoff)
            .OrderByDescending(item => item.EndedAtUtc)
            .ThenByDescending(item => item.Entry.Id, StringComparer.Ordinal)
            .Take(_retention.MaxCompletedOperations)
            .Select(item => item.Entry.Id)
            .ToHashSet(StringComparer.Ordinal);

        var evicted = completed
            .Where(item => item.EndedAtUtc <= cutoff || !retainedIds.Contains(item.Entry.Id))
            .Select(item => item.Entry)
            .ToList();
        foreach (var entry in evicted)
        {
            if (_entries.TryRemove(new KeyValuePair<string, Entry>(entry.Id, entry)))
            {
                RemoveIdempotencyForLocked(entry.Id);
                entry.Dispose();
            }
        }

        foreach (var mapping in _idempotency.ToArray())
        {
            if (!_entries.ContainsKey(mapping.Value))
            {
                _idempotency.TryRemove(new KeyValuePair<string, string>(mapping.Key, mapping.Value));
            }
        }

        return evicted.Count(entry => !_entries.ContainsKey(entry.Id));
    }

    private void RemoveIdempotencyForLocked(string operationId)
    {
        foreach (var mapping in _idempotency.ToArray())
        {
            if (string.Equals(mapping.Value, operationId, StringComparison.Ordinal))
            {
                _idempotency.TryRemove(new KeyValuePair<string, string>(mapping.Key, operationId));
            }
        }
    }

    private static string BuildIdempotencyId(string principal, string key) => string.Concat(principal, "\n", key);

    private sealed record CompletedEntry(Entry Entry, DateTimeOffset EndedAtUtc);

    public sealed class Entry : IDisposable
    {
        private const int MaxMessageLength = 512;
        private const int MaxSmallValueLength = 256;
        private const int MaxStageLength = 64;
        private static readonly Regex SecretPattern = new(
            @"(?i)\b(password|pwd|secret|token|connection(?:string)?|api[-_]?key)\s*[=:]\s*\S+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex PathPattern = new(
            @"(?i)(?:[a-z]:[\\/]|\\\\|/(?:[^ \t\r\n/]+/)+)[^ \t\r\n]*",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ExceptionPattern = new(
            @"(?i)\b(?:[a-z0-9_]+\.)*[a-z0-9_]*exception\b(?::\s*)?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly object _gate = new();
        private readonly CancellationTokenSource _cancellation = new();
        private readonly TimeProvider _timeProvider;
        private readonly int _maxEvents;
        private readonly int _maxResultArtifactIds;
        private readonly int _evidenceEventCapacity;
        private readonly int _progressEventCapacity;
        private readonly List<OperationEventDto> _evidenceEvents = [];
        private readonly Queue<OperationEventDto> _progressEvents = [];
        private readonly List<string> _resultArtifactIds = [];
        private OperationEventDto? _queuedEvent;
        private OperationEventDto? _runningEvent;
        private OperationEventDto? _terminalEvent;
        private OperationState _state;
        private int _progress;
        private string _stage = "queued";
        private DateTimeOffset? _started;
        private DateTimeOffset? _ended;
        private string? _errorCode;
        private object? _workItem;
        private bool _disposed;

        public Entry(
            string type,
            string branch,
            string principal,
            string correlation,
            object? workItem = null,
            string? destinationReference = null,
            TimeProvider? timeProvider = null,
            RuntimeRetentionPolicy? retention = null)
        {
            var policy = retention ?? RuntimeRetentionPolicy.Default;
            policy.Validate();
            Id = Guid.NewGuid().ToString("N");
            Type = BoundedValue(type, MaxSmallValueLength);
            Branch = BoundedValue(branch, MaxSmallValueLength);
            Principal = BoundedValue(principal, MaxSmallValueLength);
            Correlation = BoundedValue(correlation, MaxSmallValueLength);
            Requested = (timeProvider ?? TimeProvider.System).GetUtcNow();
            _timeProvider = timeProvider ?? TimeProvider.System;
            _maxEvents = policy.MaxEventsPerOperation;
            _maxResultArtifactIds = policy.MaxResultArtifactIdsPerOperation;
            _evidenceEventCapacity = Math.Min(4, Math.Max(0, _maxEvents - 3));
            _progressEventCapacity = Math.Max(0, _maxEvents - 3 - _evidenceEventCapacity);
            _state = OperationState.Queued;
            _workItem = workItem;
            DestinationReference = SanitizeReference(destinationReference);
            Locks = type switch
            {
                "diagnostic" or "diagnostic-destructive" => ["services"],
                "backup" => ["sql", "filesystem-cleanup"],
                _ => ["sql", "services", "filesystem-cleanup", "downloader"],
            };
            IsDestructive = type == "diagnostic-destructive";
            NeedsAudit = IsDestructive || type == "backup";
            Add("queued", "Operation queued.");
        }

        public string Id { get; }
        public string Type { get; }
        public string Branch { get; }
        public string Principal { get; }
        public string Correlation { get; }
        public DateTimeOffset Requested { get; }

        public OperationState State
        {
            get { lock (_gate) return _state; }
        }

        public int Progress
        {
            get { lock (_gate) return _progress; }
        }

        public string Stage
        {
            get { lock (_gate) return _stage; }
        }

        public DateTimeOffset? Started
        {
            get { lock (_gate) return _started; }
        }

        public DateTimeOffset? Ended
        {
            get { lock (_gate) return _ended; }
        }

        public IReadOnlyList<string> Locks { get; }
        public bool IsDestructive { get; }
        public bool NeedsAudit { get; }

        public object? WorkItem
        {
            get { lock (_gate) return _workItem; }
        }

        public string? DestinationReference { get; }
        public CancellationToken Token => _cancellation.Token;

        public bool IsActive
        {
            get
            {
                lock (_gate) return _state is OperationState.Queued or OperationState.Running;
            }
        }

        public DateTimeOffset CompletedAtUtc
        {
            get
            {
                lock (_gate) return _ended ?? Requested;
            }
        }

        public IReadOnlyList<string> ResultArtifactIds
        {
            get { lock (_gate) return [.. _resultArtifactIds]; }
        }

        public string? ErrorCode
        {
            get { lock (_gate) return _errorCode; }
        }

        public void Cancel()
        {
            lock (_gate)
            {
                if (_state is not (OperationState.Queued or OperationState.Running)) return;
                _cancellation.Cancel();
                if (_state == OperationState.Queued)
                {
                    Transition(OperationState.Cancelled);
                    _stage = "cancelled";
                    _ended = _timeProvider.GetUtcNow();
                    _workItem = null;
                    Add("cancelled", "Operation cancelled.");
                }
            }
        }

        public bool TryStart()
        {
            lock (_gate)
            {
                if (_state != OperationState.Queued) return false;
                if (_cancellation.IsCancellationRequested)
                {
                    Transition(OperationState.Cancelled);
                    _stage = "cancelled";
                    _ended = _timeProvider.GetUtcNow();
                    _workItem = null;
                    Add("cancelled", "Operation cancelled.");
                    return false;
                }

                Transition(OperationState.Running);
                _started = _timeProvider.GetUtcNow();
                _stage = "running";
                Add("running", "Operation started.");
                return true;
            }
        }

        public void Report(int progress, string stage, string message)
        {
            lock (_gate)
            {
                if (_state != OperationState.Running) return;
                _progress = Math.Clamp(Math.Max(_progress, progress), 0, 100);
                _stage = SanitizeStage(stage);
                Add(_stage, message);
            }
        }

        public void SetResultArtifacts(IEnumerable<string> artifactIds)
        {
            lock (_gate)
            {
                if (_state is not (OperationState.Queued or OperationState.Running)) return;
                _resultArtifactIds.Clear();
                _resultArtifactIds.AddRange(
                    artifactIds
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Select(id => BoundedValue(id, 64))
                        .Distinct(StringComparer.Ordinal)
                        .Take(_maxResultArtifactIds));
            }
        }

        public void Complete(OperationState finalState, string? errorCode = null)
        {
            lock (_gate)
            {
                if (finalState is not (OperationState.Cancelled or OperationState.Succeeded or OperationState.PartiallySucceeded or OperationState.Failed)) return;
                if (_state is OperationState.Cancelled or OperationState.Succeeded or OperationState.PartiallySucceeded or OperationState.Failed) return;

                if (_cancellation.IsCancellationRequested && finalState != OperationState.Cancelled)
                {
                    finalState = OperationState.Cancelled;
                    errorCode = null;
                }

                Transition(finalState);
                if (finalState == OperationState.Succeeded) _progress = 100;
                _stage = SanitizeStage(finalState.ToString().ToLowerInvariant());
                _errorCode = finalState == OperationState.Cancelled ? null : SanitizeCode(errorCode);
                _ended = _timeProvider.GetUtcNow();
                _workItem = null;
                Add(_stage, finalState == OperationState.Cancelled ? "Operation cancelled." : "Operation completed.");
            }
        }

        public OperationSummaryDto ToSummary()
        {
            lock (_gate) return new(Id, Type, _state, _progress, _stage, Requested, _started, _ended);
        }

        public ActivityRecordDto ToActivity()
        {
            lock (_gate) return new(Id, _ended ?? Requested, "operation", $"{Type}: {_state}", Correlation, IsDestructive);
        }

        public OperationDetailDto ToDto()
        {
            lock (_gate)
            {
                return new(
                    Id,
                    Type,
                    _state,
                    _progress,
                    _stage,
                    Branch,
                    Principal,
                    Requested,
                    _started,
                    _ended,
                    Locks,
                    BuildEventsLocked(),
                    [.. _resultArtifactIds],
                    _errorCode,
                    Correlation,
                    DestinationReference);
            }
        }

        public void ReleaseWorkItem()
        {
            lock (_gate) _workItem = null;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _workItem = null;
            }

            _cancellation.Dispose();
        }

        private void Transition(OperationState target)
        {
            if ((_state == OperationState.Queued && target is OperationState.Running or OperationState.Cancelled)
                || (_state == OperationState.Running && target is OperationState.Succeeded or OperationState.PartiallySucceeded or OperationState.Failed or OperationState.Cancelled))
            {
                _state = target;
                return;
            }

            throw new InvalidOperationException("Invalid operation state transition.");
        }

        private void Add(string stage, string message)
        {
            var sanitizedStage = SanitizeStage(stage);
            var current = new OperationEventDto(_timeProvider.GetUtcNow(), sanitizedStage, SanitizeMessage(message));
            switch (sanitizedStage)
            {
                case "queued":
                    _queuedEvent ??= current;
                    break;
                case "running":
                    _runningEvent ??= current;
                    break;
                case "cancelled":
                case "succeeded":
                case "partiallysucceeded":
                case "failed":
                    _terminalEvent = current;
                    break;
                case "warning":
                case "error":
                    AddBounded(_evidenceEvents, current, _evidenceEventCapacity);
                    break;
                default:
                    AddBounded(_progressEvents, current, _progressEventCapacity);
                    break;
            }
        }

        private IReadOnlyList<OperationEventDto> BuildEventsLocked()
        {
            var events = new List<OperationEventDto>(_maxEvents);
            if (_queuedEvent is not null) events.Add(_queuedEvent);
            if (_runningEvent is not null) events.Add(_runningEvent);
            events.AddRange(_evidenceEvents);
            events.AddRange(_progressEvents);
            if (_terminalEvent is not null) events.Add(_terminalEvent);
            return events.OrderBy(item => item.AtUtc).ToList();
        }

        private static void AddBounded<T>(Queue<T> queue, T value, int capacity)
        {
            if (capacity < 1) return;
            while (queue.Count >= capacity) queue.Dequeue();
            queue.Enqueue(value);
        }

        private static void AddBounded<T>(List<T> list, T value, int capacity)
        {
            if (capacity < 1) return;
            if (list.Count >= capacity) list.RemoveAt(Math.Max(0, capacity - 1));
            list.Add(value);
        }

        private static string BoundedValue(string? value, int maxLength)
        {
            var sanitized = SanitizeMessage(value);
            return sanitized.Length > maxLength ? sanitized[..maxLength] : sanitized;
        }

        private static string SanitizeStage(string? value)
        {
            var sanitized = new string((value ?? string.Empty)
                .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
                .ToArray())
                .ToLowerInvariant();
            return string.IsNullOrWhiteSpace(sanitized) ? "update" : sanitized[..Math.Min(MaxStageLength, sanitized.Length)];
        }

        private static string SanitizeMessage(string? value)
        {
            var sanitized = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            sanitized = SecretPattern.Replace(sanitized, "$1=[redacted]");
            sanitized = PathPattern.Replace(sanitized, "[redacted-path]");
            sanitized = ExceptionPattern.Replace(sanitized, "operation-error");
            return sanitized.Length > MaxMessageLength ? sanitized[..MaxMessageLength] : sanitized;
        }

        private static string? SanitizeCode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var sanitized = new string(value
                .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
                .ToArray())
                .ToLowerInvariant();
            return sanitized.Length > 128 ? sanitized[..128] : sanitized;
        }

        private static string? SanitizeReference(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var sanitized = BoundedValue(value, MaxSmallValueLength);
            if (PathPattern.IsMatch(sanitized)) return "[redacted-reference]";
            return sanitized;
        }
    }
}

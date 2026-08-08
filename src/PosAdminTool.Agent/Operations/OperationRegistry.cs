using System.Collections.Concurrent;
using System.Threading.Channels;
using PosAdminTool.Contracts.V1.Activity;
using PosAdminTool.Contracts.V1.Operations;

namespace PosAdminTool.Agent.Operations;

public sealed class OperationRegistry
{
    private const int Capacity = 32;
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _idempotency = new(StringComparer.Ordinal);
    private readonly Channel<Entry> _queue = Channel.CreateBounded<Entry>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
    });

    public event Action<OperationDetailDto>? Changed;

    public bool TryGetIdempotent(string principal, string? idempotencyKey, out OperationDetailDto? detail)
    {
        detail = null;
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return false;
        var idempotencyId = string.Concat(principal, "\n", idempotencyKey);
        return _idempotency.TryGetValue(idempotencyId, out var existing) && TryGet(existing, out detail);
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
        duplicate = false;
        var idempotencyId = string.IsNullOrWhiteSpace(idempotencyKey) ? null : string.Concat(principal, "\n", idempotencyKey);
        if (idempotencyId is not null && _idempotency.TryGetValue(idempotencyId, out var existing) && TryGet(existing, out detail))
        {
            duplicate = true;
            return true;
        }

        var entry = new Entry(operationType, branchCode, principal, correlationId, workItem, destinationReference);
        _entries[entry.Id] = entry;
        if (idempotencyId is not null && !_idempotency.TryAdd(idempotencyId, entry.Id))
        {
            _entries.TryRemove(entry.Id, out _);
            if (_idempotency.TryGetValue(idempotencyId, out var existingId) && TryGet(existingId, out detail))
            {
                duplicate = true;
                return true;
            }

            detail = null;
            return false;
        }

        if (!_queue.Writer.TryWrite(entry))
        {
            _entries.TryRemove(entry.Id, out _);
            if (idempotencyId is not null) _idempotency.TryRemove(new KeyValuePair<string, string>(idempotencyId, entry.Id));
            detail = null;
            return false;
        }

        detail = entry.ToDto();
        Publish(entry);
        return true;
    }

    public IReadOnlyList<OperationSummaryDto> List() =>
        _entries.Values.Select(entry => entry.ToSummary()).OrderByDescending(entry => entry.RequestedAtUtc).ToList();

    public IReadOnlyList<ActivityRecordDto> ListActivity() =>
        _entries.Values.Select(entry => entry.ToActivity()).OrderByDescending(entry => entry.AtUtc).ToList();

    public bool TryGet(string id, out OperationDetailDto? detail)
    {
        if (_entries.TryGetValue(id, out var entry))
        {
            detail = entry.ToDto();
            return true;
        }

        detail = null;
        return false;
    }

    public bool Cancel(string id, out OperationDetailDto? detail)
    {
        if (!_entries.TryGetValue(id, out var entry))
        {
            detail = null;
            return false;
        }

        entry.Cancel();
        detail = entry.ToDto();
        Publish(entry);
        return true;
    }

    public IAsyncEnumerable<Entry> ReadAllAsync(CancellationToken token) => _queue.Reader.ReadAllAsync(token);

    public void Publish(Entry entry) => Changed?.Invoke(entry.ToDto());

    public sealed class Entry
    {
        private readonly object _gate = new();
        private readonly CancellationTokenSource _cancellation = new();
        private readonly List<OperationEventDto> _events = [];
        private readonly List<string> _resultArtifactIds = [];

        public Entry(
            string type,
            string branch,
            string principal,
            string correlation,
            object? workItem = null,
            string? destinationReference = null)
        {
            Id = Guid.NewGuid().ToString("N");
            Type = type;
            Branch = branch;
            Principal = principal;
            Correlation = correlation;
            Requested = DateTimeOffset.UtcNow;
            State = OperationState.Queued;
            WorkItem = workItem;
            DestinationReference = destinationReference;
            Locks = type switch
            {
                "diagnostic" or "diagnostic-destructive" => ["services"],
                "backup" => ["sql", "filesystem-cleanup"],
                _ => ["sql", "services", "filesystem-cleanup", "downloader"],
            };
            IsDestructive = type == "diagnostic-destructive";
            NeedsAudit = IsDestructive || type == "backup";
            _events.Add(new(Requested, "queued", "Operation queued."));
        }

        public string Id { get; }
        public string Type { get; }
        public string Branch { get; }
        public string Principal { get; }
        public string Correlation { get; }
        public DateTimeOffset Requested { get; }
        public OperationState State { get; private set; }
        public int Progress { get; private set; }
        public string Stage { get; private set; } = "queued";
        public DateTimeOffset? Started { get; private set; }
        public DateTimeOffset? Ended { get; private set; }
        public IReadOnlyList<string> Locks { get; }
        public bool IsDestructive { get; }
        public bool NeedsAudit { get; }
        public object? WorkItem { get; }
        public string? DestinationReference { get; }
        public CancellationToken Token => _cancellation.Token;

        public IReadOnlyList<string> ResultArtifactIds
        {
            get { lock (_gate) return [.. _resultArtifactIds]; }
        }

        public string? ErrorCode { get; private set; }

        public void Cancel()
        {
            lock (_gate)
            {
                if (State is not (OperationState.Queued or OperationState.Running)) return;
                _cancellation.Cancel();
                if (State == OperationState.Queued)
                {
                    Transition(OperationState.Cancelled);
                    Stage = "cancelled";
                    Ended = DateTimeOffset.UtcNow;
                    Add(Stage, "Operation cancelled.");
                }
            }
        }

        public bool TryStart()
        {
            lock (_gate)
            {
                if (State == OperationState.Cancelled) return false;
                Transition(OperationState.Running);
                Started = DateTimeOffset.UtcNow;
                Stage = "running";
                Add(Stage, "Operation started.");
                return true;
            }
        }

        public void Report(int progress, string stage, string message)
        {
            lock (_gate)
            {
                Progress = Math.Clamp(Math.Max(Progress, progress), 0, 100);
                Stage = stage;
                Add(stage, message);
            }
        }

        public void SetResultArtifacts(IEnumerable<string> artifactIds)
        {
            lock (_gate)
            {
                _resultArtifactIds.Clear();
                _resultArtifactIds.AddRange(artifactIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal));
            }
        }

        public void Complete(OperationState finalState, string? errorCode = null)
        {
            lock (_gate)
            {
                if (finalState is not (OperationState.Cancelled or OperationState.Succeeded or OperationState.PartiallySucceeded or OperationState.Failed)) return;
                if (State is OperationState.Cancelled or OperationState.Succeeded or OperationState.PartiallySucceeded or OperationState.Failed) return;
                Transition(finalState);
                if (finalState == OperationState.Succeeded) Progress = 100;
                Stage = finalState.ToString().ToLowerInvariant();
                ErrorCode = errorCode;
                Ended = DateTimeOffset.UtcNow;
                Add(Stage, finalState == OperationState.Cancelled ? "Operation cancelled." : "Operation completed.");
            }
        }

        public OperationSummaryDto ToSummary()
        {
            lock (_gate) return new(Id, Type, State, Progress, Stage, Requested, Started, Ended);
        }

        public ActivityRecordDto ToActivity()
        {
            lock (_gate) return new(Id, Ended ?? Requested, "operation", $"{Type}: {State}", Correlation, IsDestructive);
        }

        public OperationDetailDto ToDto()
        {
            lock (_gate)
            {
                return new(
                    Id,
                    Type,
                    State,
                    Progress,
                    Stage,
                    Branch,
                    Principal,
                    Requested,
                    Started,
                    Ended,
                    Locks,
                    [.. _events],
                    [.. _resultArtifactIds],
                    ErrorCode,
                    Correlation,
                    DestinationReference);
            }
        }

        private void Transition(OperationState target)
        {
            if ((State == OperationState.Queued && target is OperationState.Running or OperationState.Cancelled)
                || (State == OperationState.Running && target is OperationState.Succeeded or OperationState.PartiallySucceeded or OperationState.Failed or OperationState.Cancelled))
            {
                State = target;
                return;
            }

            throw new InvalidOperationException("Invalid operation state transition.");
        }

        private void Add(string stage, string message) => _events.Add(new(DateTimeOffset.UtcNow, stage, Sanitize(message)));

        private static string Sanitize(string value) =>
            value.Length > 512 ? value[..512] : value.Replace("\r", " ").Replace("\n", " ");
    }
}

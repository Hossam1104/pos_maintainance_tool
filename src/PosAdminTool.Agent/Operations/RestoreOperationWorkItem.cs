using PosAdminTool.Application.Restore;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Agent.Operations;

/// <summary>
/// Server-only restore work. Disposing a queued/cancelled/completed item releases its claimed
/// upload so cancellation and shutdown cannot leave staged bytes behind.
/// </summary>
public sealed class RestoreOperationWorkItem : IDisposable
{
    private Action? _release;

    public RestoreOperationWorkItem(
        AppSettings settings,
        RestoreSourceReference source,
        RestoreMode mode,
        string expectedFingerprint,
        Action? release)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Mode = mode;
        ExpectedFingerprint = expectedFingerprint ?? string.Empty;
        _release = release;
    }

    public AppSettings Settings { get; }

    public RestoreSourceReference Source { get; }

    public RestoreMode Mode { get; }

    public string ExpectedFingerprint { get; }

    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}

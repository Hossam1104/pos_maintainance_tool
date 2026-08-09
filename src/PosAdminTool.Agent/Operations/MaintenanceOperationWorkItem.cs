using PosAdminTool.Application.Maintenance;

namespace PosAdminTool.Agent.Operations;

/// <summary>
/// Queued maintenance authority. It contains only logical mode/fingerprint evidence; the worker
/// reloads current service-owned configuration before the application service recomputes targets.
/// </summary>
public sealed class MaintenanceOperationWorkItem(
    MaintenanceMode mode,
    string expectedFingerprint) : IDisposable
{
    public MaintenanceMode Mode { get; } = mode;

    public string ExpectedFingerprint { get; } = expectedFingerprint;

    public void Dispose()
    {
        // No secret, path, or adapter resource is retained by this work item.
    }
}

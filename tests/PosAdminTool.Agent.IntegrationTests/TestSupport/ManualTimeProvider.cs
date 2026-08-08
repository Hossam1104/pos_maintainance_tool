namespace PosAdminTool.Agent.IntegrationTests.TestSupport;

/// <summary>Minimal controllable clock so handle-expiry tests don't need to sleep for real minutes.</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _utcNow = start;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan delta) => _utcNow += delta;
}

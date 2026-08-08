namespace PosAdminTool.Agent;

/// <summary>
/// The single in-memory retention policy for short-lived Agent state. Active operations and active
/// artifact downloads are never counted as evictable completed state. The limits are deliberately
/// small, explicit, and injectable so retention behavior can be verified without waiting on wall
/// clock time.
/// </summary>
public sealed record RuntimeRetentionPolicy
{
    public int MaxCompletedOperations { get; init; } = 64;

    public int MaxActivityEntries { get; init; } = 64;

    public TimeSpan CompletedOperationLifetime { get; init; } = TimeSpan.FromHours(1);

    public int MaxEventsPerOperation { get; init; } = 32;

    public int MaxResultArtifactIdsPerOperation { get; init; } = 16;

    public int MaxArtifacts { get; init; } = 64;

    public TimeSpan ArtifactLifetime { get; init; } = TimeSpan.FromHours(24);

    public int MaxFileHandles { get; init; } = 256;

    public TimeSpan FileHandleLifetime { get; init; } = TimeSpan.FromMinutes(5);

    public static RuntimeRetentionPolicy Default { get; } = new();

    public void Validate()
    {
        if (MaxCompletedOperations < 1) throw new ArgumentOutOfRangeException(nameof(MaxCompletedOperations));
        if (MaxActivityEntries < 1) throw new ArgumentOutOfRangeException(nameof(MaxActivityEntries));
        if (CompletedOperationLifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(CompletedOperationLifetime));
        if (MaxEventsPerOperation < 3) throw new ArgumentOutOfRangeException(nameof(MaxEventsPerOperation));
        if (MaxResultArtifactIdsPerOperation < 1) throw new ArgumentOutOfRangeException(nameof(MaxResultArtifactIdsPerOperation));
        if (MaxArtifacts < 1) throw new ArgumentOutOfRangeException(nameof(MaxArtifacts));
        if (ArtifactLifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ArtifactLifetime));
        if (MaxFileHandles < 1) throw new ArgumentOutOfRangeException(nameof(MaxFileHandles));
        if (FileHandleLifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(FileHandleLifetime));
    }
}

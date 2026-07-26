namespace PosAdminTool.Contracts.V1.Restore;

/// <summary>
/// <c>POST /api/v1/restores/preview</c> response. <see cref="PreviewId"/> must be presented at
/// execute time and is re-validated and re-policy-checked there — a stale or reused preview fails
/// closed (plan section 6.3, section 9).
/// </summary>
public sealed record RestorePreviewDto(
    string PreviewId,
    RestoreMode Mode,
    string TargetDatabase,
    IReadOnlyList<string> LogicalFiles,
    IReadOnlyList<string> ConfigDestinations,
    IReadOnlyList<string> ServicesToStop,
    long RequiredFreeSpaceBytes,
    IReadOnlyList<string> Warnings,
    DateTimeOffset ExpiresAtUtc);

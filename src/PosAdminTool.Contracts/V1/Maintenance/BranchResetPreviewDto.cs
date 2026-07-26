namespace PosAdminTool.Contracts.V1.Maintenance;

/// <summary><c>POST /api/v1/maintenance/reset/preview</c> response — same control set as cleanup (plan section 6.3).</summary>
public sealed record BranchResetPreviewDto(
    string ChallengeId,
    string BranchCode,
    IReadOnlyList<string> AffectedTables,
    string ConfirmationPhrase,
    DateTimeOffset ExpiresAtUtc);

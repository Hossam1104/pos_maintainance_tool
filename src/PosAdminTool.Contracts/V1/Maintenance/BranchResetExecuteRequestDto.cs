namespace PosAdminTool.Contracts.V1.Maintenance;

/// <summary><c>POST /api/v1/maintenance/reset/execute</c> request.</summary>
public sealed record BranchResetExecuteRequestDto(string ChallengeId, string TypedConfirmation);

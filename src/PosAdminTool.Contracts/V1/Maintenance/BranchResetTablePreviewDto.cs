namespace PosAdminTool.Contracts.V1.Maintenance;

public sealed record BranchResetTablePreviewDto(
    string TableName,
    long? MatchingRows);

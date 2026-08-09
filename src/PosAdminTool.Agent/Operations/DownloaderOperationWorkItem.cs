using PosAdminTool.Domain.Models;

namespace PosAdminTool.Agent.Operations;

/// <summary>
/// Server-owned downloader input. It contains no RDB password, UNC path supplied by a caller, or
/// local staging path; the worker resolves the secret and staging root internally.
/// </summary>
public sealed record DownloaderOperationWorkItem(
    AgentDownloaderConfiguration Configuration,
    IReadOnlyList<string> BranchCodes);

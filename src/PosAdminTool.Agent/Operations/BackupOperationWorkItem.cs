using PosAdminTool.Domain.Models;

namespace PosAdminTool.Agent.Operations;

/// <summary>
/// Internal-only queued backup work. It is never serialized to the browser; the destination is
/// captured only after an authorized, single-use browse handle has been redeemed.
/// </summary>
public sealed record BackupOperationWorkItem(
    AppSettings Settings,
    IReadOnlyList<string> ComponentIds,
    string DestinationPath,
    string DestinationReference);

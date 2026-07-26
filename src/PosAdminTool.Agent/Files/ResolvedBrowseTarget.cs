namespace PosAdminTool.Agent.Files;

/// <summary>
/// The result of successfully resolving a (rootId, relativeSubPath) pair. Only ever held
/// server-side — never serialized back to the browser (plan section 5.4/5.7).
/// </summary>
public sealed record ResolvedBrowseTarget(
    string RootId,
    string RelativeSubPath,
    string CanonicalFullPath,
    bool IsDirectory);

namespace PosAdminTool.Agent.Files;

/// <summary>
/// Browse roots come from configuration, never from the request (plan section 5.7). Empty by
/// default: until an operator or a later session's setup configures a root, no browse capability
/// exists at all — the safe default is zero roots, not a guessed one.
/// </summary>
public sealed class FileBrowseOptions
{
    public const string SectionName = "FileBrowse";

    public List<FileBrowseRootOptions> Roots { get; set; } = [];
}

public sealed class FileBrowseRootOptions
{
    public string RootId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string AbsolutePath { get; set; } = string.Empty;
}

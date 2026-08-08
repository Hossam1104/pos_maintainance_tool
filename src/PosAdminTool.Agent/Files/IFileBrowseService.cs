using PosAdminTool.Contracts.V1.Files;

namespace PosAdminTool.Agent.Files;

public interface IFileBrowseService
{
    /// <summary>Lists the directory at (rootId, relativeSubPath). Throws <see cref="FileBrowseValidationException"/> on any policy rejection.</summary>
    FileBrowseResultDto Browse(string rootId, string relativeSubPath);

    /// <summary>Resolves (rootId, relativeSubPath) to a real entry without listing it, for handle issuance. Throws <see cref="FileBrowseValidationException"/> on any policy rejection.</summary>
    ResolvedBrowseTarget ResolveForHandle(string rootId, string relativeSubPath);
}

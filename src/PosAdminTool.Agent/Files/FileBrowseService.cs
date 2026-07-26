using Microsoft.Extensions.Options;
using PosAdminTool.Contracts.V1.Common;
using PosAdminTool.Contracts.V1.Files;

namespace PosAdminTool.Agent.Files;

/// <summary>
/// The allowlisted server-side browse implementation from plan section 5.7. Every resolved path is
/// canonicalized and re-checked for containment within its declared root AFTER resolution; reparse
/// points, junctions, symlinks, unresolved environment variables, and parent traversal are all
/// rejected rather than followed.
/// </summary>
public sealed class FileBrowseService(IOptions<FileBrowseOptions> options) : IFileBrowseService
{
    private readonly IReadOnlyDictionary<string, FileBrowseRootOptions> _roots =
        options.Value.Roots.ToDictionary(r => r.RootId, StringComparer.Ordinal);

    public FileBrowseResultDto Browse(string rootId, string relativeSubPath)
    {
        var target = Resolve(rootId, relativeSubPath);

        if (!Directory.Exists(target.CanonicalFullPath))
        {
            throw new FileBrowseValidationException(ErrorCodes.EntryNotFound, StatusCodes.Status404NotFound);
        }

        var canonicalRoot = GetCanonicalRoot(rootId);
        var entries = new List<FileBrowseEntryDto>();

        foreach (var entryPath in Directory.EnumerateFileSystemEntries(target.CanonicalFullPath))
        {
            // A reparse point *within* an otherwise-valid directory is excluded from the listing
            // rather than followed. It is not itself a containment breach of the parent directory,
            // so the rest of the listing is still served.
            var attributes = File.GetAttributes(entryPath);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            var isDirectory = attributes.HasFlag(FileAttributes.Directory);
            var relative = Path.GetRelativePath(canonicalRoot, entryPath);

            entries.Add(new FileBrowseEntryDto(
                Name: Path.GetFileName(entryPath),
                IsDirectory: isDirectory,
                RelativeSubPath: relative,
                SizeBytes: isDirectory ? null : new FileInfo(entryPath).Length,
                LastModifiedUtc: File.GetLastWriteTimeUtc(entryPath)));
        }

        return new FileBrowseResultDto(rootId, target.RelativeSubPath, entries);
    }

    public ResolvedBrowseTarget ResolveForHandle(string rootId, string relativeSubPath)
    {
        var target = Resolve(rootId, relativeSubPath);

        if (!File.Exists(target.CanonicalFullPath) && !Directory.Exists(target.CanonicalFullPath))
        {
            throw new FileBrowseValidationException(ErrorCodes.EntryNotFound, StatusCodes.Status404NotFound);
        }

        return target;
    }

    private ResolvedBrowseTarget Resolve(string rootId, string relativeSubPath)
    {
        if (!_roots.TryGetValue(rootId, out var root))
        {
            throw new FileBrowseValidationException(ErrorCodes.UnknownBrowseRoot, StatusCodes.Status400BadRequest);
        }

        relativeSubPath ??= string.Empty;

        // Reject unresolved environment variables outright rather than expand them ourselves.
        if (relativeSubPath.Contains('%'))
        {
            throw new FileBrowseValidationException(ErrorCodes.UnresolvedEnvironmentVariable, StatusCodes.Status400BadRequest);
        }

        // Reject absolute paths, drive-qualified paths, UNC paths, and NTFS alternate-data-stream
        // syntax (':' outside position 1 has no legitimate use in a relative sub-path here).
        if (Path.IsPathRooted(relativeSubPath) || relativeSubPath.Contains(':'))
        {
            throw new FileBrowseValidationException(ErrorCodes.AbsolutePathRejected, StatusCodes.Status400BadRequest);
        }

        // Reject explicit parent traversal segments before any combination happens.
        var segments = relativeSubPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == ".."))
        {
            throw new FileBrowseValidationException(ErrorCodes.PathTraversalRejected, StatusCodes.Status400BadRequest);
        }

        var canonicalRoot = GetCanonicalRoot(rootId);
        var combined = Path.Combine(root.AbsolutePath, relativeSubPath);
        var canonicalTarget = Path.GetFullPath(combined);

        // Canonicalize first, THEN re-check containment — the check that actually matters, since
        // the string-level checks above are defense in depth, not the guarantee.
        var isWithinRoot = canonicalTarget.Equals(canonicalRoot, StringComparison.OrdinalIgnoreCase)
            || canonicalTarget.StartsWith(canonicalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        if (!isWithinRoot)
        {
            throw new FileBrowseValidationException(ErrorCodes.PathEscapesRoot, StatusCodes.Status400BadRequest);
        }

        if (HasReparsePointInChain(canonicalRoot, canonicalTarget))
        {
            throw new FileBrowseValidationException(ErrorCodes.ReparsePointRejected, StatusCodes.Status400BadRequest);
        }

        var relativeResult = Path.GetRelativePath(canonicalRoot, canonicalTarget);
        var isDirectory = Directory.Exists(canonicalTarget);

        return new ResolvedBrowseTarget(rootId, relativeResult == "." ? string.Empty : relativeResult, canonicalTarget, isDirectory);
    }

    private string GetCanonicalRoot(string rootId) => Path.GetFullPath(_roots[rootId].AbsolutePath);

    private static bool HasReparsePointInChain(string canonicalRootPath, string canonicalTargetPath)
    {
        var normalizedRoot = canonicalRootPath.TrimEnd(Path.DirectorySeparatorChar);
        var current = canonicalTargetPath;

        while (!string.IsNullOrEmpty(current))
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
            {
                return true;
            }

            var normalizedCurrent = current.TrimEnd(Path.DirectorySeparatorChar);
            if (string.Equals(normalizedCurrent, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }

        return false;
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;

namespace PosAdminTool.Agent.IntegrationTests;

/// <summary>
/// Standing regression check for the Session 01 determinism requirement: no floating dependency
/// version anywhere in the tree (plan section 5.1 / session prompts Session 01, task 3).
/// </summary>
public partial class NoWildcardDependencyVersionTests
{
    [Fact]
    public void NoCsprojReferencesAWildcardPackageVersion()
    {
        var repoRoot = FindRepoRoot();
        var offenders = new List<string>();

        foreach (var file in EnumerateRepoFiles(repoRoot, "*.csproj"))
        {
            var content = File.ReadAllText(file);
            if (WildcardCsprojVersionPattern().IsMatch(content))
            {
                offenders.Add(Path.GetRelativePath(repoRoot, file));
            }
        }

        Assert.True(offenders.Count == 0, $"Wildcard package version(s) found in: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void NoPackageJsonDeclaresARangedDependencyVersion()
    {
        var repoRoot = FindRepoRoot();
        var offenders = new List<string>();

        foreach (var file in EnumerateRepoFiles(repoRoot, "package.json"))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));

            foreach (var section in new[] { "dependencies", "devDependencies" })
            {
                if (!document.RootElement.TryGetProperty(section, out var deps))
                {
                    continue;
                }

                foreach (var dep in deps.EnumerateObject())
                {
                    var version = dep.Value.GetString() ?? string.Empty;
                    if (IsRangedOrWildcard(version))
                    {
                        offenders.Add($"{Path.GetRelativePath(repoRoot, file)}: {dep.Name}@{version}");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, $"Ranged/wildcard npm dependency version(s) found: {string.Join(", ", offenders)}");
    }

    private static bool IsRangedOrWildcard(string version) =>
        version.Length > 0 &&
        (version[0] is '^' or '~' or '>' or '<' || version.Contains('*') || version.Contains("||") || version.Contains(" - "));

    private static IEnumerable<string> EnumerateRepoFiles(string repoRoot, string searchPattern) =>
        Directory.EnumerateFiles(repoRoot, searchPattern, SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}dist{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PosAdminTool.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root (PosAdminTool.sln not found).");
    }

    [GeneratedRegex(@"Version\s*=\s*""[^""]*\*""")]
    private static partial Regex WildcardCsprojVersionPattern();
}

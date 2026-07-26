using System.Reflection;
using PosAdminTool.Contracts.V1.Common;

namespace PosAdminTool.Agent.IntegrationTests;

/// <summary>
/// Structural guarantee, not an intention: no contract type can carry a password, token, or
/// connection string anywhere, and no REQUEST contract can accept a raw/absolute file-system path
/// — assert both via reflection over every public property in the Contracts assembly (Session 02
/// required tests), rather than merely reviewing each DTO by eye.
///
/// Response DTOs reporting a server-computed path for operator transparency (e.g.
/// <c>CleanupPreviewDto.PathsToDelete</c> — plan section 8.7 explicitly requires showing the exact
/// paths a destructive operation will affect) are legitimate and out of scope for the path check;
/// the rule (plan section 5.2) is about never ACCEPTING one from the browser.
/// </summary>
public class ContractShapeTests
{
    // The only two properties allowed to carry a secret: write-only fields on update requests,
    // used to SET a new secret. They are never returned in any response (see
    // RedactedConfigurationDto.HasSqlPassword / RedactedDownloaderConfigurationDto.HasRdbPassword,
    // which are deliberately bool, not string, and so are already excluded by the string-shaped
    // filter below).
    private static readonly HashSet<(string TypeName, string PropertyName)> AllowedSecretCarriers =
    [
        ("ConfigurationUpdateRequestDto", "SqlPassword"),
        ("DownloaderConfigurationUpdateRequestDto", "RdbPassword"),
    ];

    private static readonly string[] BannedSubstrings =
    [
        "password", "secret", "connectionstring", "accesstoken", "bearertoken",
        "uncpath", "absolutepath", "fullpath", "filepath",
    ];

    [Fact]
    public void NoContractTypeCarriesASecretOutsideTheTwoWriteOnlyUpdateFields()
    {
        var offenders = new List<string>();

        foreach (var type in GetContractDtoTypes())
        {
            foreach (var property in StringShapedProperties(type))
            {
                var lowerName = property.Name.ToLowerInvariant();
                var isBanned = BannedSubstrings.Any(banned => lowerName.Contains(banned));
                var isAllowed = AllowedSecretCarriers.Contains((type.Name, property.Name));

                if (isBanned && !isAllowed)
                {
                    offenders.Add($"{type.Name}.{property.Name}");
                }
            }
        }

        Assert.True(offenders.Count == 0, $"Contract propert(y/ies) carrying a secret/token/connection-string-shaped value: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void NoRequestContractAcceptsARawOrAbsoluteFileSystemPath()
    {
        var offenders = new List<string>();

        foreach (var type in GetContractDtoTypes().Where(t => t.Name.EndsWith("RequestDto", StringComparison.Ordinal)))
        {
            foreach (var property in StringShapedProperties(type))
            {
                var lowerName = property.Name.ToLowerInvariant();
                if (lowerName.Contains("path") && !lowerName.Contains("sub"))
                {
                    offenders.Add($"{type.Name}.{property.Name}");
                }
            }
        }

        Assert.True(offenders.Count == 0, $"Request contract propert(y/ies) accepting a raw/absolute path: {string.Join(", ", offenders)}");
    }

    private static IEnumerable<PropertyInfo> StringShapedProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => IsStringOrStringCollection(p.PropertyType));

    private static bool IsStringOrStringCollection(Type type) =>
        type == typeof(string)
        || type == typeof(string[])
        || (type.IsGenericType && type.GetGenericArguments() is [var arg] && arg == typeof(string));

    private static IEnumerable<Type> GetContractDtoTypes() =>
        typeof(ErrorCodes).Assembly
            .GetTypes()
            .Where(t => t.IsPublic && (t.IsClass || t.IsValueType) && !t.IsEnum && !t.IsAbstract);
}

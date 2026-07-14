namespace PosAdminTool.Maui.Services;

internal static class SemanticColorResolver
{
    public static Color Success() => Resolve("SuccessColor", Color.FromArgb("#6FD08C"));

    public static Color Warning() => Resolve("WarningColor", Color.FromArgb("#F4C85D"));

    public static Color Danger() => Resolve("DangerColor", Color.FromArgb("#FF96A1"));

    public static Color Info() => Resolve("InfoColor", Color.FromArgb("#7AB8FF"));

    private static Color Resolve(string tokenName, Color fallback)
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        var resources = app?.Resources;
        if (resources is null)
        {
            return fallback;
        }

        var theme = app?.UserAppTheme ?? AppTheme.Unspecified;
        if (theme == AppTheme.Unspecified)
        {
            theme = app?.RequestedTheme ?? AppTheme.Dark;
        }

        var themedKey = theme == AppTheme.Light ? $"Light{tokenName}" : $"Dark{tokenName}";
        return resources.TryGetValue(themedKey, out var themedValue) && themedValue is Color color
            ? color
            : fallback;
    }
}
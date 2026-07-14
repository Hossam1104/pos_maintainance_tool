using Microsoft.Extensions.DependencyInjection;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Infrastructure.Windows;

namespace PosAdminTool.Maui;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly bool _exitAfterElevation;

    public App(IServiceProvider services, IConfigurationService configurationService, AdminPrivilegeManager adminPrivilegeManager)
    {
        Services = services;
        InitializeComponent();
        ApplySavedTheme(configurationService);

        if (!adminPrivilegeManager.IsAdministrator() && adminPrivilegeManager.RequestAdministrator())
        {
            _exitAfterElevation = true;
        }
    }

    public static IServiceProvider Services { get; private set; } = null!;

    public static T Resolve<T>()
        where T : notnull
    {
        return Services.GetRequiredService<T>();
    }

    private void ApplySavedTheme(IConfigurationService configurationService)
    {
        try
        {
            var settings = configurationService.LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            UserAppTheme = ResolveTheme(settings.Theme);
        }
        catch
        {
            UserAppTheme = AppTheme.Light;
        }
    }

    internal static AppTheme ResolveTheme(string? theme)
    {
        return string.Equals(theme, "DARK", StringComparison.OrdinalIgnoreCase)
            ? AppTheme.Dark
            : AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = _exitAfterElevation
            ? new Window(new ContentPage())
            : new MainWindow();

        window.Width = 1600;
        window.Height = 940;
        window.MinimumWidth = 1480;
        window.MinimumHeight = 860;

        if (_exitAfterElevation)
        {
            window.Dispatcher.Dispatch(Quit);
        }

        return window;
    }
}

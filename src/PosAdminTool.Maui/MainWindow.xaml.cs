using PosAdminTool.Domain.Interfaces;

namespace PosAdminTool.Maui;

public partial class MainWindow : Window
{
    private readonly IConfigurationService _configurationService;

    public MainWindow()
    {
        InitializeComponent();

        _configurationService = App.Resolve<IConfigurationService>();
        ShellHost.Navigated += OnShellNavigated;

        if (Microsoft.Maui.Controls.Application.Current is not null)
        {
            Microsoft.Maui.Controls.Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
            UpdateThemeStatus(Microsoft.Maui.Controls.Application.Current.RequestedTheme);
        }

        UpdateSectionContext();
    }

    private async void OnToggleThemeClicked(object? sender, EventArgs e)
    {
        var settings = await _configurationService.LoadAsync().ConfigureAwait(false);
        settings.Theme = string.Equals(settings.Theme, "DARK", StringComparison.OrdinalIgnoreCase) ? "LIGHT" : "DARK";
        await _configurationService.SaveAsync(settings).ConfigureAwait(false);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var theme = App.ResolveTheme(settings.Theme);
            if (Microsoft.Maui.Controls.Application.Current is not null)
            {
                Microsoft.Maui.Controls.Application.Current.UserAppTheme = theme;
            }

            UpdateThemeStatus(theme);
        });
    }

    private async void OnOpenLogClicked(object? sender, EventArgs e)
    {
        await ShellHost.GoToAsync("//log");
    }

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        UpdateSectionContext();
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        UpdateThemeStatus(e.RequestedTheme);
    }

    private void UpdateThemeStatus(AppTheme theme)
    {
        var label = theme == AppTheme.Light ? "Light" : "Dark";
        if (ThemeStatusLabel is not null)
        {
            ThemeStatusLabel.Text = label;
        }
        
        if (WindowTitleBar is not null)
        {
            WindowTitleBar.Subtitle = $"Windows operations shell · {label}";
        }

        if (ThemeButton is not null)
        {
            ThemeButton.Text = theme == AppTheme.Light ? "☀️  ●  " : "  ●  🌙";
        }
    }

    private void UpdateSectionContext()
    {
        var title = GetCurrentShellContent()?.Title ?? "Configuration";
        var (code, description) = title switch
        {
            "Configuration" => ("CFG", "Branch identity, SQL settings, and automation credentials."),
            "Services" => ("SVC", "Live service status and Windows control actions."),
            "Operations" => ("OPS", "Backup, restore, cleanup, and guarded maintenance."),
            "DB Queries" => ("SQL", "Remote client queries and POS cart automation."),
            "Log" => ("LOG", "Live operational telemetry and event history."),
            _ => ("DBS", "Operational workspace.")
        };

        SectionCodeLabel.Text = code;
        SectionTitleLabel.Text = title;
        SectionDescriptionLabel.Text = description;
    }

    private ShellContent? GetCurrentShellContent()
    {
        return ShellHost.CurrentItem?.CurrentItem?.CurrentItem;
    }


}
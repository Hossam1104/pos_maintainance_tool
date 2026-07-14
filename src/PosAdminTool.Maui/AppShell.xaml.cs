using System.Collections.ObjectModel;

namespace PosAdminTool.Maui;

public partial class AppShell : Shell
{
    private bool _isUpdatingSelection;

    public ObservableCollection<ShellNavItem> FlyoutEntries { get; } =
    [
        new("Configuration", "configuration", "\uE713"),
        new("Services", "services", "\uE895"),
        new("Operations", "operations", "\uE7C3"),
        new("DB Queries", "dbqueries", "\uE721"),
        new("Log", "log", "\uE81C")
    ];

    public AppShell()
    {
        InitializeComponent();

        Navigated += OnShellNavigated;
        UpdateSelection("configuration");
    }

    private async void OnFlyoutSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection || e.CurrentSelection.FirstOrDefault() is not ShellNavItem item)
        {
            return;
        }

        var target = $"//{item.Route}";
        if (string.Equals(CurrentState?.Location.OriginalString, target, StringComparison.OrdinalIgnoreCase))
        {
            UpdateSelection(item.Route);
            return;
        }

        await GoToAsync(target);
        UpdateSelection(item.Route);
    }

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        UpdateSelection(e.Current.Location.OriginalString);
    }

    private void UpdateSelection(string? location)
    {
        var route = ResolveRoute(location);
        var selectedItem = FlyoutEntries.FirstOrDefault(item => string.Equals(item.Route, route, StringComparison.OrdinalIgnoreCase));

        _isUpdatingSelection = true;
        foreach (var item in FlyoutEntries)
        {
            item.IsSelected = ReferenceEquals(item, selectedItem);
        }

        FlyoutItemsView.SelectedItem = selectedItem;
        _isUpdatingSelection = false;
    }

    private static string ResolveRoute(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return "configuration";
        }

        return location.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "configuration";
    }
}

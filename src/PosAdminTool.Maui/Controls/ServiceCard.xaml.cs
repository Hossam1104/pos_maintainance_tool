using System.Windows.Input;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Maui.Services;

namespace PosAdminTool.Maui.Controls;

public partial class ServiceCard : ContentView
{
    private bool _hasAnimated;

    public static readonly BindableProperty ServiceNameProperty = BindableProperty.Create(nameof(ServiceName), typeof(string), typeof(ServiceCard), string.Empty);
    public static readonly BindableProperty StatusProperty = BindableProperty.Create(nameof(Status), typeof(ServiceStatus), typeof(ServiceCard), ServiceStatus.Unknown);
    public static readonly BindableProperty StartCommandProperty = BindableProperty.Create(nameof(StartCommand), typeof(ICommand), typeof(ServiceCard));
    public static readonly BindableProperty StopCommandProperty = BindableProperty.Create(nameof(StopCommand), typeof(ICommand), typeof(ServiceCard));
    public static readonly BindableProperty RestartCommandProperty = BindableProperty.Create(nameof(RestartCommand), typeof(ICommand), typeof(ServiceCard));

    public ServiceCard()
    {
        InitializeComponent();
        this.EnableStoreInteractions(includeBorders: true);
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (_hasAnimated)
        {
            return;
        }

        _hasAnimated = true;
        await this.AnimateEntranceAsync(360, 0, 18, 0.985);
    }

    public string ServiceName
    {
        get => (string)GetValue(ServiceNameProperty);
        set => SetValue(ServiceNameProperty, value);
    }

    public ServiceStatus Status
    {
        get => (ServiceStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public ICommand? StartCommand
    {
        get => (ICommand?)GetValue(StartCommandProperty);
        set => SetValue(StartCommandProperty, value);
    }

    public ICommand? StopCommand
    {
        get => (ICommand?)GetValue(StopCommandProperty);
        set => SetValue(StopCommandProperty, value);
    }

    public ICommand? RestartCommand
    {
        get => (ICommand?)GetValue(RestartCommandProperty);
        set => SetValue(RestartCommandProperty, value);
    }
}

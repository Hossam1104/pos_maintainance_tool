using PosAdminTool.Maui.ViewModels;
using PosAdminTool.Maui.Services;

namespace PosAdminTool.Maui.Views;

public partial class LogPage : ContentPage
{
    public LogPage()
    {
        InitializeComponent();
        BindingContext = App.Resolve<LogViewModel>();
        AnimatedRoot.EnableStoreInteractions();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await AnimatedRoot.AnimateStorePageAsync();
    }
}

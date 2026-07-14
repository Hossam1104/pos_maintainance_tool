using PosAdminTool.Maui.ViewModels;
using PosAdminTool.Maui.Services;

namespace PosAdminTool.Maui.Views;

public partial class ConfigurationPage : ContentPage
{
    private ConfigurationViewModel ViewModel => (ConfigurationViewModel)BindingContext;

    public ConfigurationPage()
    {
        InitializeComponent();
        BindingContext = App.Resolve<ConfigurationViewModel>();
        AnimatedRoot.EnableStoreInteractions();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadCommand.ExecuteAsync(null);
        await AnimatedRoot.AnimateStorePageAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ViewModel.Unload();
    }
}

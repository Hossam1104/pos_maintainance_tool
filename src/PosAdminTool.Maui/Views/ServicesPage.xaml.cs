using PosAdminTool.Maui.ViewModels;
using PosAdminTool.Maui.Services;

namespace PosAdminTool.Maui.Views;

public partial class ServicesPage : ContentPage
{
    private ServicesViewModel ViewModel => (ServicesViewModel)BindingContext;

    public ServicesPage()
    {
        InitializeComponent();
        BindingContext = App.Resolve<ServicesViewModel>();
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
        ViewModel.StopTimer();
    }
}

using PosAdminTool.Maui.ViewModels;
using PosAdminTool.Maui.Services;

namespace PosAdminTool.Maui.Views;

public partial class OperationsPage : ContentPage
{
    private OperationsViewModel ViewModel => (OperationsViewModel)BindingContext;

    public OperationsPage()
    {
        InitializeComponent();
        BindingContext = App.Resolve<OperationsViewModel>();
        AnimatedRoot.EnableStoreInteractions();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadCommand.ExecuteAsync(null);
        await AnimatedRoot.AnimateStorePageAsync();
    }
}

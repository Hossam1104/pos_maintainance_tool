using PosAdminTool.Maui.ViewModels;
using PosAdminTool.Maui.Services;

namespace PosAdminTool.Maui.Views;

public partial class DbQueriesPage : ContentPage
{
    private DbQueriesViewModel ViewModel => (DbQueriesViewModel)BindingContext;

    public DbQueriesPage()
    {
        InitializeComponent();
        BindingContext = App.Resolve<DbQueriesViewModel>();
        AnimatedRoot.EnableStoreInteractions();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadCommand.ExecuteAsync(null);
        await AnimatedRoot.AnimateStorePageAsync();
    }
}

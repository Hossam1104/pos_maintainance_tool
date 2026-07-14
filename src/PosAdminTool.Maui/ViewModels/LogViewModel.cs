using CommunityToolkit.Mvvm.Input;
using PosAdminTool.Maui.Services;

namespace PosAdminTool.Maui.ViewModels;

public sealed partial class LogViewModel : BaseViewModel
{
    public LogViewModel(LogHub logHub)
        : base(logHub)
    {
        Hub = logHub;
    }

    public LogHub Hub { get; }

    [RelayCommand]
    private void ClearLog()
    {
        Hub.Clear();
    }
}

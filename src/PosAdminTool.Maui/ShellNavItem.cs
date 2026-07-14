using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PosAdminTool.Maui;

public sealed class ShellNavItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public ShellNavItem(string title, string route, string glyph)
    {
        Title = title;
        Route = route;
        Glyph = glyph;
    }

    public string Title { get; }

    public string Route { get; }

    public string Glyph { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
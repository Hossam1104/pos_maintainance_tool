using System.Globalization;
using PosAdminTool.Maui.Services;

namespace PosAdminTool.Maui.Converters;

public sealed class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? SemanticColorResolver.Success() : SemanticColorResolver.Danger();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

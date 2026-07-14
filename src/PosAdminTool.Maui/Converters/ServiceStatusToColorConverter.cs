using System.Globalization;
using PosAdminTool.Domain.Enums;
using PosAdminTool.Maui.Services;

namespace PosAdminTool.Maui.Converters;

public sealed class ServiceStatusToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ServiceStatus.Running => SemanticColorResolver.Success(),
            ServiceStatus.Stopped => SemanticColorResolver.Warning(),
            ServiceStatus.NotFound => SemanticColorResolver.Danger(),
            _ => SemanticColorResolver.Info()
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

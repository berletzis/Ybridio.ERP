using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Ybridio.WinUI.Converters;

/// <summary>
/// Convierte bool a Visibility con lógica directa:
/// true → Visible, false → Collapsed.
/// Usado para mostrar elementos cuando una condición es verdadera.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
            return b ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility v)
            return v == Visibility.Visible;
        return false;
    }
}

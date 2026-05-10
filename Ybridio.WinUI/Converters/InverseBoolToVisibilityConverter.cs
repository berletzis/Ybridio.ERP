using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Ybridio.WinUI.Converters;

/// <summary>
/// Convierte bool a Visibility con lógica inversa:
/// true → Collapsed, false → Visible.
/// Usado para ocultar elementos cuando una condición es verdadera.
/// </summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
            return b ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility v)
            return v == Visibility.Collapsed;
        return false;
    }
}

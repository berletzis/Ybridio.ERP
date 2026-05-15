using Microsoft.UI.Xaml.Data;
using System;

namespace Ybridio.WinUI.Converters;

/// <summary>
/// Convierte bool → "Sí" / "No" para display operacional en grids documentales.
/// Uso institucional: columnas IVA, AplicaIva, etc.
/// PROHIBIDO mostrar "True/False" en la UI operacional.
/// </summary>
public sealed class BoolToSiNoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && b ? "Sí" : "No";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is string s && s == "Sí";
}

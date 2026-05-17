using Microsoft.UI.Xaml.Data;
using System;

namespace Ybridio.WinUI.Converters;

/// <summary>
/// Formatea un <see cref="decimal"/> con el formato indicado en ConverterParameter (ej. "N2", "G").
/// Si no se especifica parámetro, usa "N2" (dos decimales, sin símbolo monetario).
/// </summary>
public sealed class DecimalFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var fmt = parameter as string ?? "N2";
        return value is decimal d ? d.ToString(fmt) : value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

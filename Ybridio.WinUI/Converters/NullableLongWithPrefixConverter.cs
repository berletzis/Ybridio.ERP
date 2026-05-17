using Microsoft.UI.Xaml.Data;
using System;

namespace Ybridio.WinUI.Converters;

/// <summary>
/// Formatea un <see cref="long?"/> con un prefijo textual.
/// Si el valor es <c>null</c> devuelve "—"; de lo contrario devuelve "{ConverterParameter}{valor}".
/// Uso típico: PedidoId con ConverterParameter="PED-" → "PED-55" o "—".
/// </summary>
public sealed class NullableLongWithPrefixConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is null or DBNull) return "—";
        var prefix = parameter as string ?? string.Empty;
        return $"{prefix}{value}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

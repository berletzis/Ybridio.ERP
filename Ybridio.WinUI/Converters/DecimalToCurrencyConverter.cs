using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace Ybridio.WinUI.Converters;

/// <summary>
/// Convierte un <see cref="decimal"/> a texto monetario institucional ($#,##0.00).
/// </summary>
/// <remarks>
/// Aplica el Financial Formatting Standard (Operational Grid Standard v2, ADR-035):
/// todo valor financiero visible debe incluir símbolo monetario y punto decimal fijo.
/// Usar siempre junto a <c>OgCellFinancialStyle</c> o <c>OgCurrencyTextStyle</c>.
/// </remarks>
public sealed class DecimalToCurrencyConverter : IValueConverter
{
    private static readonly CultureInfo _culture = new("es-MX");

    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is decimal d)
            return d.ToString("C", _culture);

        if (value is double dbl)
            return ((decimal)dbl).ToString("C", _culture);

        return "$0.00";
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Windows.UI;

namespace Ybridio.WinUI.Converters;

/// <summary>
/// Convierte estado de stock (string: "Normal" | "Bajo" | "Agotado") a Color discreto.
/// </summary>
public sealed class StockStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var estado = value as string ?? "Normal";

        return estado switch
        {
            "Agotado" => Color.FromArgb(255, 196, 43, 28),  // #C42B1C rojo
            "Bajo"    => Color.FromArgb(255, 224, 128, 0),  // #E08000 amber
            _         => Color.FromArgb(255, 115, 115, 115) // #737373 gris
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

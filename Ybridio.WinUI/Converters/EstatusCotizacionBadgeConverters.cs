using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Ybridio.Domain.Ventas;

namespace Ybridio.WinUI.Converters;

/// <summary>
/// Convierte <see cref="EstatusCotizacion"/> al color de fondo del badge de estado (paleta OgStatus* de OperationalGridBase).
/// </summary>
public sealed class EstatusCotizacionToBgBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var estatus = value is EstatusCotizacion e ? e : EstatusCotizacion.Borrador;

        var color = estatus switch
        {
#pragma warning disable CS0618
            EstatusCotizacion.Enviada    => Color.FromArgb(255, 235, 243, 251), // Legacy → Aprobada bg
#pragma warning restore CS0618
            EstatusCotizacion.Aprobada   => Color.FromArgb(255, 235, 243, 251), // #EBF3FB — azul bg
            EstatusCotizacion.Convertida => Color.FromArgb(255, 224, 242, 230), // #E0F2E6 — verde tenue bg
            EstatusCotizacion.Cancelada  => Color.FromArgb(255, 248, 248, 248), // #F8F8F8 — gris tenue bg
            _                            => Color.FromArgb(255, 240, 240, 240), // #F0F0F0 — borrador bg
        };

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

/// <summary>
/// Convierte <see cref="EstatusCotizacion"/> al color de texto del badge de estado (paleta OgStatus* de OperationalGridBase).
/// </summary>
public sealed class EstatusCotizacionToFgBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var estatus = value is EstatusCotizacion e ? e : EstatusCotizacion.Borrador;

        var color = estatus switch
        {
#pragma warning disable CS0618
            EstatusCotizacion.Enviada    => Color.FromArgb(255,   0, 120, 212), // Legacy → Aprobada text
#pragma warning restore CS0618
            EstatusCotizacion.Aprobada   => Color.FromArgb(255,   0, 120, 212), // #0078D4 — azul text
            EstatusCotizacion.Convertida => Color.FromArgb(255,   0, 128,  64), // #008040 — verde text
            EstatusCotizacion.Cancelada  => Color.FromArgb(255, 160, 160, 160), // #A0A0A0 — gris tenue text
            _                            => Color.FromArgb(255, 138, 138, 138), // #8A8A8A — borrador text
        };

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

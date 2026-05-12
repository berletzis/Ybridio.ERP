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
            EstatusCotizacion.Enviada  => Color.FromArgb(255, 255, 244, 204), // #FFF4CC — ámbar bg
            EstatusCotizacion.Aprobada => Color.FromArgb(255, 235, 243, 251), // #EBF3FB — azul bg
            EstatusCotizacion.Cancelada => Color.FromArgb(255, 248, 248, 248), // #F8F8F8 — gris tenue bg
            _                          => Color.FromArgb(255, 240, 240, 240), // #F0F0F0 — borrador bg
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
            EstatusCotizacion.Enviada  => Color.FromArgb(255, 196, 127,   0), // #C47F00 — ámbar text
            EstatusCotizacion.Aprobada => Color.FromArgb(255,   0, 120, 212), // #0078D4 — azul text
            EstatusCotizacion.Cancelada => Color.FromArgb(255, 160, 160, 160), // #A0A0A0 — gris tenue text
            _                          => Color.FromArgb(255, 138, 138, 138), // #8A8A8A — borrador text
        };

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

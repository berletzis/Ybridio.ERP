using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Ybridio.Domain.Ventas;

namespace Ybridio.WinUI.Converters;

/// <summary>
/// Convierte <see cref="EstatusVenta"/> al color de fondo del badge de estado.
/// Paleta: Borrador=gris, PendientePago=ámbar, Pagada=azul, Facturada/Entregada=verde, Cerrada=verde oscuro, Cancelada=gris oscuro.
/// </summary>
public sealed class EstatusVentaToBgBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var estatus = value is EstatusVenta e ? e : EstatusVenta.Borrador;

        var color = estatus switch
        {
            EstatusVenta.Borrador      => Color.FromArgb(255, 240, 240, 240), // #F0F0F0 — gris tenue
            EstatusVenta.PendientePago => Color.FromArgb(255, 255, 244, 206), // #FFF4CE — ámbar (pendiente)
            EstatusVenta.Pagada        => Color.FromArgb(255, 235, 243, 251), // #EBF3FB — azul tenue (pagada)
            EstatusVenta.Facturada     => Color.FromArgb(255, 224, 242, 230), // #E0F2E6 — verde tenue
            EstatusVenta.Entregada     => Color.FromArgb(255, 205, 237, 219), // #CDEDDB — verde medio
            EstatusVenta.Cerrada       => Color.FromArgb(255, 180, 225, 200), // #B4E1C8 — verde sólido tenue
            EstatusVenta.Cancelada     => Color.FromArgb(255, 248, 248, 248), // #F8F8F8 — gris muy tenue
            _                          => Color.FromArgb(255, 240, 240, 240),
        };

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

/// <summary>
/// Convierte <see cref="EstatusVenta"/> al color de texto del badge de estado.
/// </summary>
public sealed class EstatusVentaToFgBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var estatus = value is EstatusVenta e ? e : EstatusVenta.Borrador;

        var color = estatus switch
        {
            EstatusVenta.Borrador      => Color.FromArgb(255, 138, 138, 138), // #8A8A8A — gris
            EstatusVenta.PendientePago => Color.FromArgb(255, 138, 100,   0), // #8A6400 — ámbar
            EstatusVenta.Pagada        => Color.FromArgb(255,   0, 120, 212), // #0078D4 — azul
            EstatusVenta.Facturada     => Color.FromArgb(255,   0, 128,  64), // #008040 — verde
            EstatusVenta.Entregada     => Color.FromArgb(255,   0,  96,  48), // #006030 — verde oscuro
            EstatusVenta.Cerrada       => Color.FromArgb(255,   0,  64,  32), // #004020 — verde muy oscuro
            EstatusVenta.Cancelada     => Color.FromArgb(255, 160, 160, 160), // #A0A0A0 — gris tenue
            _                          => Color.FromArgb(255, 138, 138, 138),
        };

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

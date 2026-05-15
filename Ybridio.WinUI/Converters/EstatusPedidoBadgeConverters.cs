using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Ybridio.Domain.Ventas;

namespace Ybridio.WinUI.Converters;

/// <summary>
/// Convierte <see cref="EstatusPedido"/> al color de fondo del badge de estado.
/// Paleta: Borrador=gris, Autorizado=azul, EnProceso=ámbar, Parcial=naranja, Finalizado=verde, Cancelado=gris oscuro.
/// </summary>
public sealed class EstatusPedidoToBgBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var estatus = value is EstatusPedido e ? e : EstatusPedido.Borrador;

        var color = estatus switch
        {
            EstatusPedido.Borrador   => Color.FromArgb(255, 240, 240, 240), // #F0F0F0 — gris tenue
            EstatusPedido.Autorizado => Color.FromArgb(255, 235, 243, 251), // #EBF3FB — azul tenue
            EstatusPedido.EnProceso  => Color.FromArgb(255, 255, 244, 206), // #FFF4CE — ámbar tenue
            EstatusPedido.Parcial    => Color.FromArgb(255, 254, 229, 202), // #FEE5CA — naranja tenue
            EstatusPedido.Finalizado => Color.FromArgb(255, 224, 242, 230), // #E0F2E6 — verde tenue
            EstatusPedido.Cancelado  => Color.FromArgb(255, 248, 248, 248), // #F8F8F8 — gris muy tenue
            _                        => Color.FromArgb(255, 240, 240, 240),
        };

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

/// <summary>
/// Convierte <see cref="EstatusPedido"/> al color de texto del badge de estado.
/// </summary>
public sealed class EstatusPedidoToFgBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var estatus = value is EstatusPedido e ? e : EstatusPedido.Borrador;

        var color = estatus switch
        {
            EstatusPedido.Borrador   => Color.FromArgb(255, 138, 138, 138), // #8A8A8A — gris
            EstatusPedido.Autorizado => Color.FromArgb(255,   0, 120, 212), // #0078D4 — azul
            EstatusPedido.EnProceso  => Color.FromArgb(255, 138, 100,   0), // #8A6400 — ámbar
            EstatusPedido.Parcial    => Color.FromArgb(255, 138,  56,   0), // #8A3800 — naranja
            EstatusPedido.Finalizado => Color.FromArgb(255,   0, 128,  64), // #008040 — verde
            EstatusPedido.Cancelado  => Color.FromArgb(255, 160, 160, 160), // #A0A0A0 — gris tenue
            _                        => Color.FromArgb(255, 138, 138, 138),
        };

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

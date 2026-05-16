using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Ybridio.Domain.Ventas;

namespace Ybridio.WinUI.Converters;

/// <summary>
/// Convierte <see cref="EstadoFinancieroPedido"/> al color de fondo del badge financiero.
/// Paleta: SinPago=gris, AnticipoParcial=ámbar, AnticipoCompleto=azul, ParcialmentePagado=naranja, Liquidado=verde, SobrePagado=violeta.
/// </summary>
public sealed class EstadoFinancieroPedidoToBgBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var estado = value is EstadoFinancieroPedido e ? e : EstadoFinancieroPedido.SinPago;

        var color = estado switch
        {
            EstadoFinancieroPedido.SinPago            => Color.FromArgb(255, 240, 240, 240), // #F0F0F0 — gris
            EstadoFinancieroPedido.AnticipoParcial    => Color.FromArgb(255, 255, 244, 206), // #FFF4CE — ámbar
            EstadoFinancieroPedido.AnticipoCompleto   => Color.FromArgb(255, 235, 243, 251), // #EBF3FB — azul
            EstadoFinancieroPedido.ParcialmentePagado => Color.FromArgb(255, 254, 229, 202), // #FEE5CA — naranja
            EstadoFinancieroPedido.Liquidado          => Color.FromArgb(255, 224, 242, 230), // #E0F2E6 — verde
            EstadoFinancieroPedido.SobrePagado        => Color.FromArgb(255, 237, 224, 254), // #EDE0FE — violeta
            _                                         => Color.FromArgb(255, 240, 240, 240),
        };

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

/// <summary>
/// Convierte <see cref="EstadoFinancieroPedido"/> al color de texto del badge financiero.
/// </summary>
public sealed class EstadoFinancieroPedidoToFgBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var estado = value is EstadoFinancieroPedido e ? e : EstadoFinancieroPedido.SinPago;

        var color = estado switch
        {
            EstadoFinancieroPedido.SinPago            => Color.FromArgb(255, 138, 138, 138), // #8A8A8A — gris
            EstadoFinancieroPedido.AnticipoParcial    => Color.FromArgb(255, 138, 100,   0), // #8A6400 — ámbar
            EstadoFinancieroPedido.AnticipoCompleto   => Color.FromArgb(255,   0, 120, 212), // #0078D4 — azul
            EstadoFinancieroPedido.ParcialmentePagado => Color.FromArgb(255, 138,  56,   0), // #8A3800 — naranja
            EstadoFinancieroPedido.Liquidado          => Color.FromArgb(255,   0, 128,  64), // #008040 — verde
            EstadoFinancieroPedido.SobrePagado        => Color.FromArgb(255, 107,  33, 168), // #6B21A8 — violeta
            _                                         => Color.FromArgb(255, 138, 138, 138),
        };

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

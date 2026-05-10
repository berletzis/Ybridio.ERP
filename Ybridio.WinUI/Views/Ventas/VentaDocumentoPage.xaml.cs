using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Application.Services.Venta;
using Ybridio.Domain.Ventas;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.Services.Workspace;
using Ybridio.WinUI.ViewModels.Ventas;

namespace Ybridio.WinUI.Views.Ventas;

/// <summary>
/// Page de documento de venta PYME.
/// Reutiliza el patrón WorkspaceTab de PedidoDocumentoPage/CotizacionDocumentoPage.
/// </summary>
public sealed partial class VentaDocumentoPage : Page
{
    private readonly IWorkspaceService      _workspace;
    private readonly IPedidoService         _pedidoService;
    public VentaDocumentoViewModel ViewModel { get; }

    public VentaDocumentoPage(VentaDocumentalDto? venta)
    {
        ViewModel = new VentaDocumentoViewModel(
            App.Services.GetRequiredService<IVentaDocumentalService>(),
            App.Services.GetRequiredService<IErpAuthorizationService>(),
            App.Services.GetRequiredService<SessionService>(),
            App.Services.GetRequiredService<ICurrentContextTracker>());

        _workspace     = App.Services.GetRequiredService<IWorkspaceService>();
        _pedidoService = App.Services.GetRequiredService<IPedidoService>();

        InitializeComponent();
        ViewModel.Initialize(venta);

        // Sincronizar ComboBox TipoPago con el ViewModel
        CombTipoPago.SelectedIndex = (int)ViewModel.TipoPagoVenta;
    }

    private void CombTipoPago_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CombTipoPago.SelectedIndex >= 0)
            ViewModel.TipoPagoVenta = (TipoPago)CombTipoPago.SelectedIndex;
    }

    /// <summary>Abre el Pedido origen de esta venta en el Workspace, si existe.</summary>
    private async void BtnAbrirPedidoOrigen_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.PedidoOrigenId is not { } pedidoId) return;
        await _workspace.OpenOrActivateDocumentTabAsync(
            key:         $"pedido-{pedidoId}",
            title:       $"Pedido #{pedidoId}",
            icon:        "",
            dataLoader:  () => _pedidoService.ObtenerConDetallesAsync(pedidoId)
                                .ContinueWith(t => t.Result.Success ? t.Result.Value : null),
            pageFactory: dto => new PedidoDocumentoPage(dto!),
            onError:     err => ViewModel.ErrorMessage = err,
            isClosable:  true);
    }

    private async void BtnAgregarLinea_Click(object sender, RoutedEventArgs e)
    {
        var detalle = await MostrarDialogoNuevaLinea();
        if (detalle is not null)
            await ViewModel.AgregarDetalleLocalAsync(detalle);
    }

    private async void BtnRegistrarPago_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeRegistrarPago)
        {
            ViewModel.ErrorMessage = "La venta debe estar Confirmada para registrar un pago.";
            return;
        }

        var txtMonto     = new TextBox { PlaceholderText = "Monto", Text = ViewModel.SaldoPendiente.ToString("F2") };
        var cmbFormaPago = new ComboBox { Width = double.NaN };
        cmbFormaPago.Items.Add(new ComboBoxItem { Content = "Efectivo",      Tag = "Efectivo" });
        cmbFormaPago.Items.Add(new ComboBoxItem { Content = "Transferencia", Tag = "Transferencia" });
        cmbFormaPago.Items.Add(new ComboBoxItem { Content = "Cheque",        Tag = "Cheque" });
        cmbFormaPago.Items.Add(new ComboBoxItem { Content = "Otro",          Tag = "Otro" });
        cmbFormaPago.SelectedIndex = 0;
        var txtRef = new TextBox { PlaceholderText = "Referencia (opcional)" };

        void Lbl(StackPanel p, string t) => p.Children.Add(new TextBlock { Text = t, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var panel = new StackPanel { Spacing = 8 };
        Lbl(panel, "Monto *"); panel.Children.Add(txtMonto);
        Lbl(panel, "Forma de pago *"); panel.Children.Add(cmbFormaPago);
        Lbl(panel, "Referencia"); panel.Children.Add(txtRef);

        var dialog = new ContentDialog
        {
            Title = "Registrar Pago", PrimaryButtonText = "Registrar", SecondaryButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot, Content = panel
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (!decimal.TryParse(txtMonto.Text, out var monto) || monto <= 0)
        {
            ViewModel.ErrorMessage = "Monto invalido."; return;
        }
        var forma = (cmbFormaPago.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Efectivo";
        await ViewModel.RegistrarPagoAsync(monto, forma, txtRef.Text.Trim().NullIfEmpty());
    }

    private async void BtnCancelarVenta_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeCancelar) { ViewModel.ErrorMessage = "Esta venta no se puede cancelar."; return; }
        var dialog = new ContentDialog
        {
            Title = "Confirmar cancelacion", Content = "Cancelar esta venta?",
            PrimaryButtonText = "Si", SecondaryButtonText = "No",
            DefaultButton = ContentDialogButton.Secondary, XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.CancelarVentaAsync();
    }

    private async System.Threading.Tasks.Task<DetalleLineaEditable?> MostrarDialogoNuevaLinea()
    {
        var txtDesc   = new TextBox { PlaceholderText = "Descripcion" };
        var txtQty    = new TextBox { PlaceholderText = "Cantidad", Text = "1" };
        var txtPrecio = new TextBox { PlaceholderText = "Precio unitario", Text = "0" };
        var panel = new StackPanel { Spacing = 10 };
        void Lbl(string t) => panel.Children.Add(new TextBlock { Text = t, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        Lbl("Descripcion *"); panel.Children.Add(txtDesc);
        Lbl("Cantidad *");    panel.Children.Add(txtQty);
        Lbl("Precio *");      panel.Children.Add(txtPrecio);
        var dialog = new ContentDialog
        {
            Title = "Nueva Linea", PrimaryButtonText = "Agregar", SecondaryButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot, Content = panel
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
        var desc = txtDesc.Text.Trim();
        if (string.IsNullOrEmpty(desc)) { ViewModel.ErrorMessage = "Descripcion obligatoria."; return null; }
        if (!decimal.TryParse(txtQty.Text, out var qty) || qty <= 0) { ViewModel.ErrorMessage = "Cantidad invalida."; return null; }
        if (!decimal.TryParse(txtPrecio.Text, out var precio) || precio < 0) { ViewModel.ErrorMessage = "Precio invalido."; return null; }
        return new DetalleLineaEditable(0, null, desc, qty, precio);
    }
}

internal static class VentaPageStringExtensions
{
    internal static string? NullIfEmpty(this string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}

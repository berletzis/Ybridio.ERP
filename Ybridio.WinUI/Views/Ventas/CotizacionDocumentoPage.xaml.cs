using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Venta;
using Ybridio.Domain.Ventas;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Workspace;
using Ybridio.WinUI.ViewModels.Ventas;
using XamlApp = Microsoft.UI.Xaml.Application;

namespace Ybridio.WinUI.Views.Ventas;

/// <summary>
/// Documento de cotización persistente en WorkspaceService.
/// Soporta nueva cotización (flujo completo) y edición de existente.
/// </summary>
public sealed partial class CotizacionDocumentoPage : Page
{
    private readonly IWorkspaceService _workspace;
    public CotizacionDocumentoViewModel ViewModel { get; }

    public CotizacionDocumentoPage(CotizacionDto? cotizacion)
    {
        ViewModel  = new CotizacionDocumentoViewModel(
            App.Services.GetRequiredService<ICotizacionService>(),
            App.Services.GetRequiredService<Ybridio.Application.Services.Autorizacion.IErpAuthorizationService>(),
            App.Services.GetRequiredService<SessionService>(),
            App.Services.GetRequiredService<Ybridio.WinUI.Services.Diagnostic.IOperationalObservabilityService>(),
            App.Services.GetRequiredService<Ybridio.WinUI.Services.Diagnostic.ICurrentContextTracker>());

        _workspace = App.Services.GetRequiredService<IWorkspaceService>();
        InitializeComponent();

        ViewModel.NotificarPedidoGenerado = AbrirPedidoEnWorkspace;
        ViewModel.Initialize(cotizacion);
    }

    private void AbrirPedidoEnWorkspace(PedidoDto pedido)
    {
        _workspace.OpenTab(
            key:         $"pedido-{pedido.Id}",
            title:       $"Pedido #{pedido.Id}",
            icon:        "",
            pageFactory: () => new PedidoDocumentoPage(pedido),
            isClosable:  true);
    }

    // ── Handlers de CommandBar ────────────────────────────────────────────────

    private async void BtnAgregarLinea_Click(object sender, RoutedEventArgs e)
    {
        var detalle = await MostrarDialogoNuevaLinea();
        if (detalle is null) return;
        await ViewModel.AgregarDetalleLocalAsync(detalle);
    }

    private async void BtnEnviar_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeEnviar) { ViewModel.ErrorMessage = "Solo se puede enviar una cotización en estado Borrador."; return; }
        await ViewModel.CambiarEstatusCommand.ExecuteAsync(EstatusCotizacion.Enviada);
    }

    private async void BtnAprobar_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeAprobar) { ViewModel.ErrorMessage = "Solo se puede aprobar una cotización Enviada."; return; }
        await ViewModel.CambiarEstatusCommand.ExecuteAsync(EstatusCotizacion.Aprobada);
    }

    private async void BtnCancelarCotizacion_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeCancelar) { ViewModel.ErrorMessage = "Esta cotización no se puede cancelar en su estado actual."; return; }
        var dialog = new ContentDialog
        {
            Title = "Confirmar cancelación",
            Content = "¿Cancelar esta cotización? Esta acción no es reversible.",
            PrimaryButtonText = "Sí, cancelar", SecondaryButtonText = "No",
            DefaultButton = ContentDialogButton.Secondary, XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.CambiarEstatusCommand.ExecuteAsync(EstatusCotizacion.Cancelada);
    }

    // ── Diálogo: Agregar Línea ────────────────────────────────────────────────

    private async System.Threading.Tasks.Task<DetalleLineaEditable?> MostrarDialogoNuevaLinea()
    {
        var txtDescripcion    = new TextBox { PlaceholderText = "Descripción del ítem o servicio" };
        var txtCantidad       = new TextBox { PlaceholderText = "Cantidad",       Text = "1" };
        var txtPrecioUnitario = new TextBox { PlaceholderText = "Precio unitario",Text = "0" };

        var panel = new StackPanel { Spacing = 10 };
        void Lbl(string t) => panel.Children.Add(new TextBlock { Text = t, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        Lbl("Descripción *"); panel.Children.Add(txtDescripcion);
        Lbl("Cantidad *");    panel.Children.Add(txtCantidad);
        Lbl("Precio *");      panel.Children.Add(txtPrecioUnitario);

        var dialog = new ContentDialog
        {
            Title = "Nueva Línea", PrimaryButtonText = "Agregar", SecondaryButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot, Content = panel
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;

        var desc = txtDescripcion.Text.Trim();
        if (string.IsNullOrEmpty(desc)) { ViewModel.ErrorMessage = "La descripción es obligatoria."; return null; }
        if (!decimal.TryParse(txtCantidad.Text.Trim(), out var qty) || qty <= 0) { ViewModel.ErrorMessage = "Cantidad inválida."; return null; }
        if (!decimal.TryParse(txtPrecioUnitario.Text.Trim(), out var precio) || precio < 0) { ViewModel.ErrorMessage = "Precio inválido."; return null; }

        return new DetalleLineaEditable(0, null, desc, qty, precio);
    }
}

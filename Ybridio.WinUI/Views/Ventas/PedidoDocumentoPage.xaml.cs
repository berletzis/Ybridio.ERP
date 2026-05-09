using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Venta;
using Ybridio.Domain.Ventas;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Workspace;
using Ybridio.WinUI.ViewModels.Ventas;

namespace Ybridio.WinUI.Views.Ventas;

public sealed partial class PedidoDocumentoPage : Page
{
    private readonly IWorkspaceService _workspace;
    public PedidoDocumentoViewModel ViewModel { get; }

    public PedidoDocumentoPage(PedidoDto? pedido)
    {
        ViewModel  = new PedidoDocumentoViewModel(
            App.Services.GetRequiredService<IPedidoService>(),
            App.Services.GetRequiredService<Ybridio.Application.Services.Autorizacion.IErpAuthorizationService>(),
            App.Services.GetRequiredService<SessionService>(),
            App.Services.GetRequiredService<Ybridio.WinUI.Services.Diagnostic.IOperationalObservabilityService>(),
            App.Services.GetRequiredService<Ybridio.WinUI.Services.Diagnostic.ICurrentContextTracker>());
        _workspace = App.Services.GetRequiredService<IWorkspaceService>();
        InitializeComponent();
        ViewModel.NotificarOTGenerada = AbrirOTEnWorkspace;
        ViewModel.Initialize(pedido);
    }

    private void AbrirOTEnWorkspace(OrdenTrabajoDto ot)
    {
        _workspace.OpenTab(
            key:         $"ot-{ot.Id}",
            title:       $"OT #{ot.Id}",
            icon:        "",
            pageFactory: () => new OrdenTrabajoDocumentoPage(ot),
            isClosable:  true);
    }

    private async void BtnAgregarLinea_Click(object sender, RoutedEventArgs e)
    {
        var detalle = await MostrarDialogoNuevaLinea();
        if (detalle is not null) await ViewModel.AgregarDetalleLocalAsync(detalle);
    }

    private async void BtnGenerarOT_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeGenerarOT) { ViewModel.ErrorMessage = "El pedido debe estar Confirmado o En Proceso para generar una OT."; return; }
        var txtDescripcion = new TextBox { PlaceholderText = "Descripcion del trabajo a realizar", AcceptsReturn = true };
        var dialog = new ContentDialog
        {
            Title = "Generar Orden de Trabajo", PrimaryButtonText = "Generar", SecondaryButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot,
            Content = new StackPanel { Spacing = 8, Children = {
                new TextBlock { Text = "Descripcion del trabajo *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                txtDescripcion } }
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (string.IsNullOrWhiteSpace(txtDescripcion.Text)) { ViewModel.ErrorMessage = "La descripcion es obligatoria."; return; }
        await ViewModel.GenerarOrdenTrabajoCommand.ExecuteAsync(txtDescripcion.Text.Trim());
    }

    private async void BtnCancelarPedido_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeCancelar) { ViewModel.ErrorMessage = "Este pedido no se puede cancelar."; return; }
        var dialog = new ContentDialog
        {
            Title = "Confirmar cancelacion", Content = "Cancelar este pedido?",
            PrimaryButtonText = "Si", SecondaryButtonText = "No",
            DefaultButton = ContentDialogButton.Secondary, XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.CancelarAsync();
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

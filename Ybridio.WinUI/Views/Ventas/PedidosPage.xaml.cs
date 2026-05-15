using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.Services.Venta;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels.Ventas;

namespace Ybridio.WinUI.Views.Ventas;

/// <summary>
/// Host de Pedidos — sigue el Document Surface UX Pattern (ADR-025 + ADR-031).
/// Los documentos se abren inline reemplazando el listado, NO como tabs de workspace.
/// </summary>
public sealed partial class PedidosPage : Page, ILiveContextReporter
{
    private readonly IPedidoService _pedidoService;
    public PedidosViewModel ViewModel { get; }

    public PedidosPage()
    {
        ViewModel      = App.Services.GetRequiredService<PedidosViewModel>();
        _pedidoService = App.Services.GetRequiredService<IPedidoService>();
        InitializeComponent();

        // Guard extra: sincronizar visibilidad del status bar con el surface documental.
        // El x:Bind en XAML ya lo hace pero este suscriptor garantiza que no haya timing issues.
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PedidosViewModel.IsDocumentSurfaceVisible))
                ActualizarVisibilidadStatusBar();
        };
    }

    private void ActualizarVisibilidadStatusBar()
    {
        StatusBarPedidos.Visibility = ViewModel.IsDocumentSurfaceVisible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.RefrescarCommand.CanExecute(null))
            await ViewModel.RefrescarCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.DetachFromContext();
    }

    public void ReportLiveContext() => ViewModel.ReportLiveContext();

    /// <summary>
    /// Abre un Pedido como Document Surface inline, invocado desde la conversión de Cotización.
    /// Mismo comportamiento que abrir desde el grid — EsInlineMode = true.
    /// </summary>
    public void AbrirPedidoDesdeConversion(Ybridio.Application.DTOs.Ventas.PedidoDto pedido)
    {
        var page = new PedidoDocumentoPage(pedido);
        page.VolverALista = () => _ = ViewModel.CerrarDocumentSurfaceAsync();
        page.EsInlineMode = true;
        ViewModel.AbrirEditarPedido(page);
    }

    private void BtnNuevo_Click(object sender, RoutedEventArgs e)
    {
        // ADR-031: nuevo documento se abre inline como Document Surface, NO como workspace tab
        var page = new PedidoDocumentoPage((Ybridio.Application.DTOs.Ventas.PedidoDto?)null);
        page.VolverALista  = () => _ = ViewModel.CerrarDocumentSurfaceAsync();
        page.EsInlineMode  = true;
        ViewModel.AbrirNuevoPedido(page);
    }

    private async void BtnAbrir_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.PedidoSeleccionado is null) return;
        await AbrirPedidoInlineAsync(ViewModel.PedidoSeleccionado.Id);
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.PedidoSeleccionado is null) return;
        _ = AbrirPedidoInlineAsync(ViewModel.PedidoSeleccionado.Id);
    }

    private async System.Threading.Tasks.Task AbrirPedidoInlineAsync(long id)
    {
        ViewModel.IsBusy = true;
        ViewModel.ErrorMessage = string.Empty;
        try
        {
            var result = await _pedidoService.ObtenerConDetallesAsync(id);
            if (!result.Success)
            {
                ViewModel.ErrorMessage = result.Error ?? "Error al cargar el pedido.";
                return;
            }
            // ADR-031: documento se carga como surface inline, SIN workspace tab
            var page = new PedidoDocumentoPage(result.Value);
            page.VolverALista = () => _ = ViewModel.CerrarDocumentSurfaceAsync();
            page.EsInlineMode = true;
            ViewModel.AbrirEditarPedido(page);
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }
}

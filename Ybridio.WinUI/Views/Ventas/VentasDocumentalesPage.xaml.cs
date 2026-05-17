using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Venta;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels.Ventas;

namespace Ybridio.WinUI.Views.Ventas;

/// <summary>
/// Host de Ventas Documentales — sigue el Document Surface UX Pattern (ADR-025 + ADR-031).
/// Los documentos se abren inline reemplazando el listado, NO como tabs de workspace.
/// </summary>
public sealed partial class VentasDocumentalesPage
{
    private readonly IVentaDocumentalService _ventaService;

    public VentasDocumentalesViewModel ViewModel { get; }

    /// <summary>
    /// Instancia activa de VentasDocumentalesPage (navegada). Null si el usuario no está en este sub-módulo.
    /// Usado por workflows externos (ej. PedidoDocumentoPage) para abrir una venta inline sin workspace tab (ADR-031).
    /// </summary>
    public static VentasDocumentalesPage? Current { get; private set; }

    public VentasDocumentalesPage()
    {
        ViewModel     = App.Services.GetRequiredService<VentasDocumentalesViewModel>();
        _ventaService = App.Services.GetRequiredService<IVentaDocumentalService>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Current = this;
        try
        {
            await ViewModel.RefrescarCommand.ExecuteAsync(null);
        }
        catch (OperationCanceledException)
        {
            // ADR-026: expected during navigation/lifecycle transitions.
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (Current == this) Current = null;
        ViewModel.DetachFromContext();
    }

    /// <summary>
    /// Abre una venta existente como Document Surface inline.
    /// Invocado desde workflows externos (ej. PedidoDocumentoPage tras generar venta — ADR-031).
    /// </summary>
    public Task AbrirVentaDesdeWorkflowAsync(VentaDocumentalDto venta)
    {
        var page = new VentaDocumentoPage(venta);
        page.OnCerrar = async () =>
        {
            try { await ViewModel.CerrarDocumentSurfaceAsync(); }
            catch (OperationCanceledException) { }
        };
        ViewModel.AbrirDocumentoVenta(page);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void ReportLiveContext() => ViewModel.ReportLiveContext();

    private void BtnNuevo_Click(object sender, RoutedEventArgs e)
    {
        // ADR-031: nueva venta se abre inline como Document Surface, NO como workspace tab
        var page = new VentaDocumentoPage(null);
        page.OnCerrar = async () =>
        {
            try { await ViewModel.CerrarDocumentSurfaceAsync(); }
            catch (OperationCanceledException) { /* ADR-026: expected lifecycle cancellation. */ }
        };
        ViewModel.AbrirDocumentoVenta(page);
    }

    private async void BtnAbrir_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.VentaSeleccionada is null) return;
        try
        {
            await AbrirVentaInlineAsync(ViewModel.VentaSeleccionada.Id);
        }
        catch (OperationCanceledException)
        {
            // ADR-026: expected lifecycle cancellation during navigation.
        }
    }

    private async void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.VentaSeleccionada is null) return;
        try
        {
            await AbrirVentaInlineAsync(ViewModel.VentaSeleccionada.Id);
        }
        catch (OperationCanceledException)
        {
            // ADR-026: expected lifecycle cancellation during navigation.
        }
    }

    private async void Busqueda_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        try
        {
            await ViewModel.RefrescarCommand.ExecuteAsync(null);
        }
        catch (OperationCanceledException)
        {
            // ADR-026: expected during rapid query input/navigation.
        }
    }

    private async void FiltroTemporal_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            await ViewModel.RefrescarCommand.ExecuteAsync(null);
        }
        catch (OperationCanceledException)
        {
            // ADR-026: expected during filter changes/lifecycle transitions.
        }
    }

    private async System.Threading.Tasks.Task AbrirVentaInlineAsync(long id)
    {
        ViewModel.IsBusy = true;
        ViewModel.ErrorMessage = string.Empty;
        try
        {
            var result = await _ventaService.ObtenerConDetallesAsync(id);
            if (!result.Success) { ViewModel.ErrorMessage = result.Error ?? "Error al cargar."; return; }

            // ADR-031: documento se carga como surface inline, SIN workspace tab
            var page = new VentaDocumentoPage(result.Value);
            page.OnCerrar = async () =>
            {
                try { await ViewModel.CerrarDocumentSurfaceAsync(); }
                catch (OperationCanceledException) { /* ADR-026: expected lifecycle cancellation. */ }
            };
            ViewModel.AbrirDocumentoVenta(page);
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }
}

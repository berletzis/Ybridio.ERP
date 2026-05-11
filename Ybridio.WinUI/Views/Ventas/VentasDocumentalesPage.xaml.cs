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
/// Host de Ventas Documentales — sigue el Document Surface UX Pattern (ADR-025 + ADR-031).
/// Los documentos se abren inline reemplazando el listado, NO como tabs de workspace.
/// </summary>
public sealed partial class VentasDocumentalesPage : Page, ILiveContextReporter
{
    private readonly IVentaDocumentalService _ventaService;

    public VentasDocumentalesViewModel ViewModel { get; }

    public VentasDocumentalesPage()
    {
        ViewModel     = App.Services.GetRequiredService<VentasDocumentalesViewModel>();
        _ventaService = App.Services.GetRequiredService<IVentaDocumentalService>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.RefrescarCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.DetachFromContext();
    }

    /// <inheritdoc/>
    public void ReportLiveContext() => ViewModel.ReportLiveContext();

    private void BtnNuevo_Click(object sender, RoutedEventArgs e)
    {
        // ADR-031: nueva venta se abre inline como Document Surface, NO como workspace tab
        var page = new VentaDocumentoPage(null);
        page.OnCerrar = async () => await ViewModel.CerrarDocumentSurfaceAsync();
        ViewModel.AbrirDocumentoVenta(page);
    }

    private async void BtnAbrir_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.VentaSeleccionada is null) return;
        await AbrirVentaInlineAsync(ViewModel.VentaSeleccionada.Id);
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.VentaSeleccionada is null) return;
        _ = AbrirVentaInlineAsync(ViewModel.VentaSeleccionada.Id);
    }

    private async void Busqueda_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        => await ViewModel.RefrescarCommand.ExecuteAsync(null);

    private async void FiltroTemporal_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => await ViewModel.RefrescarCommand.ExecuteAsync(null);

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
            page.OnCerrar = async () => await ViewModel.CerrarDocumentSurfaceAsync();
            ViewModel.AbrirDocumentoVenta(page);
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }
}

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.Services.Venta;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.Services.Workspace;
using Ybridio.WinUI.ViewModels.Ventas;

namespace Ybridio.WinUI.Views.Ventas;

public sealed partial class CotizacionesPage : Page, ILiveContextReporter
{
    private readonly IWorkspaceService   _workspace;
    private readonly ICotizacionService  _cotizacionService;
    public CotizacionesViewModel ViewModel { get; }

    public CotizacionesPage()
    {
        ViewModel          = App.Services.GetRequiredService<CotizacionesViewModel>();
        _workspace         = App.Services.GetRequiredService<IWorkspaceService>();
        _cotizacionService = App.Services.GetRequiredService<ICotizacionService>();
        InitializeComponent();
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

    // ── Document Surface UX Pattern Handlers ─────────────────────────────────

    /// <summary>
    /// Abre el Document Surface para crear una nueva cotización.
    /// Reemplaza el comportamiento anterior de abrir una nueva Workspace Tab.
    /// </summary>
    private void BtnNueva_Click(object sender, RoutedEventArgs e)
    {
        var page = new CotizacionDocumentoPage(null);
        page.ViewModel.DocumentSaved = OnDocumentSaved;
        page.VolverALista = OnVolverALista;
        ViewModel.DocumentSurfaceContent = page;
        ViewModel.IsDocumentSurfaceVisible = true;
    }

    /// <summary>
    /// Abre el Document Surface para editar la cotización seleccionada.
    /// Reemplaza el comportamiento anterior de abrir una Workspace Tab persistente.
    /// </summary>
    private async void BtnAbrir_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CotizacionSeleccionada is null) return;
        await AbrirCotizacionEnDocumentSurface(ViewModel.CotizacionSeleccionada.Id);
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.CotizacionSeleccionada is null) return;
        _ = AbrirCotizacionEnDocumentSurface(ViewModel.CotizacionSeleccionada.Id);
    }

    private async System.Threading.Tasks.Task AbrirCotizacionEnDocumentSurface(long id)
    {
        var result = await _cotizacionService.ObtenerConDetallesAsync(id);
        if (!result.Success || result.Value is null)
        {
            ViewModel.ErrorMessage = result.Error ?? "No se pudo cargar la cotización.";
            return;
        }
        var page = new CotizacionDocumentoPage(result.Value);
        page.ViewModel.DocumentSaved = OnDocumentSaved;
        page.VolverALista = OnVolverALista;
        ViewModel.DocumentSurfaceContent = page;
        ViewModel.IsDocumentSurfaceVisible = true;
    }

    /// <summary>
    /// Callback invocado cuando el documento se guarda exitosamente.
    /// Cierra el Document Surface y refresca el grid de listado.
    /// </summary>
    private async void OnDocumentSaved()
    {
        await ViewModel.CerrarDocumentSurfaceAsync();
        ViewModel.SuccessMessage = "Cotización guardada correctamente.";
    }

    /// <summary>
    /// Callback invocado cuando el usuario hace clic en "← Volver a Lista".
    /// Cierra el Document Surface sin guardar y vuelve al grid de listado.
    /// </summary>
    private async void OnVolverALista()
    {
        await ViewModel.CerrarDocumentSurfaceAsync();
    }
}

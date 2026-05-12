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

    // ── Document Surface UX Pattern Handlers (ADR-032) ───────────────────────

    private void BtnNueva_Click(object sender, RoutedEventArgs e)
    {
        var page = new CotizacionDocumentoPage(null);
        page.VolverALista   = OnVolverALista;
        page.EsInlineMode   = true;
        ViewModel.DocumentSurfaceContent   = page;
        ViewModel.IsDocumentSurfaceVisible = true;
    }

    private async void BtnAbrir_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CotizacionSeleccionada is null) return;
        await AbrirCotizacionInline(ViewModel.CotizacionSeleccionada.Id);
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.CotizacionSeleccionada is null) return;
        _ = AbrirCotizacionInline(ViewModel.CotizacionSeleccionada.Id);
    }

    private async System.Threading.Tasks.Task AbrirCotizacionInline(long id)
    {
        var result = await _cotizacionService.ObtenerConDetallesAsync(id);
        if (!result.Success || result.Value is null)
        {
            ViewModel.ErrorMessage = result.Error ?? "No se pudo cargar la cotización.";
            return;
        }
        var page = new CotizacionDocumentoPage(result.Value);
        page.VolverALista   = OnVolverALista;
        page.EsInlineMode   = true;
        ViewModel.DocumentSurfaceContent   = page;
        ViewModel.IsDocumentSurfaceVisible = true;
    }

    private async void OnVolverALista()
    {
        await ViewModel.CerrarDocumentSurfaceAsync();
    }
}

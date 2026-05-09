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

    private void BtnNueva_Click(object sender, RoutedEventArgs e)
    {
        var tempKey = $"cotizacion-nueva-{Guid.NewGuid():N}";
        _workspace.OpenTab(
            key:         tempKey,
            title:       "Nueva Cotizacion",
            icon:        "",
            pageFactory: () => new CotizacionDocumentoPage(null),
            isClosable:  true);
    }

    private async void BtnAbrir_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CotizacionSeleccionada is null) return;
        await AbrirCotizacionEnWorkspace(ViewModel.CotizacionSeleccionada.Id);
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.CotizacionSeleccionada is null) return;
        _ = AbrirCotizacionEnWorkspace(ViewModel.CotizacionSeleccionada.Id);
    }

    private async System.Threading.Tasks.Task AbrirCotizacionEnWorkspace(long id)
    {
        var key = $"cotizacion-{id}";
        if (_workspace.Exists(key)) { _workspace.ActivateTab(key); return; }

        var result = await _cotizacionService.ObtenerConDetallesAsync(id);
        if (!result.Success) { ViewModel.ErrorMessage = result.Error ?? "Error al cargar."; return; }

        _workspace.OpenTab(
            key:         key,
            title:       $"Cotizacion #{id}",
            icon:        "",
            pageFactory: () => new CotizacionDocumentoPage(result.Value),
            isClosable:  true);
    }
}

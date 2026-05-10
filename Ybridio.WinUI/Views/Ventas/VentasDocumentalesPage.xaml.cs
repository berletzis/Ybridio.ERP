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

/// <summary>
/// Grid list de ventas documentales.
/// Abre documentos individuales en el Workspace. Sigue el patrón de PedidosPage.
/// </summary>
public sealed partial class VentasDocumentalesPage : Page, ILiveContextReporter
{
    private readonly IWorkspaceService       _workspace;
    private readonly IVentaDocumentalService _ventaService;

    public VentasDocumentalesViewModel ViewModel { get; }

    public VentasDocumentalesPage()
    {
        ViewModel     = App.Services.GetRequiredService<VentasDocumentalesViewModel>();
        _workspace    = App.Services.GetRequiredService<IWorkspaceService>();
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
        var key = $"venta-nueva-{System.Guid.NewGuid():N}";
        _workspace.OpenTab(
            key:         key,
            title:       "Nueva Venta",
            icon:        "",
            pageFactory: () => new VentaDocumentoPage(null),
            isClosable:  true);
    }

    private async void BtnAbrir_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.VentaSeleccionada is null) return;
        await AbrirVentaEnWorkspaceAsync(ViewModel.VentaSeleccionada.Id);
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.VentaSeleccionada is null) return;
        _ = AbrirVentaEnWorkspaceAsync(ViewModel.VentaSeleccionada.Id);
    }

    private async void Busqueda_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        => await ViewModel.RefrescarCommand.ExecuteAsync(null);

    private async void FiltroTemporal_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => await ViewModel.RefrescarCommand.ExecuteAsync(null);

    private async System.Threading.Tasks.Task AbrirVentaEnWorkspaceAsync(long id)
    {
        var key = $"venta-{id}";
        if (_workspace.Exists(key)) { _workspace.ActivateTab(key); return; }

        var result = await _ventaService.ObtenerConDetallesAsync(id);
        if (!result.Success) { ViewModel.ErrorMessage = result.Error ?? "Error al cargar."; return; }

        _workspace.OpenTab(
            key:         key,
            title:       $"Venta #{id}",
            icon:        "",
            pageFactory: () => new VentaDocumentoPage(result.Value),
            isClosable:  true);
    }
}

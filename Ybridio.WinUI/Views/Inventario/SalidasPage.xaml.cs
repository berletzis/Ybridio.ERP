using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.Services.Venta;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.Services.Workspace;
using Ybridio.WinUI.ViewModels.Inventario;
using Ybridio.WinUI.Views.Ventas;

namespace Ybridio.WinUI.Views.Inventario;

public sealed partial class SalidasPage : Page, ILiveContextReporter
{
    private readonly IWorkspaceService           _workspace;
    private readonly IVentaDocumentalService     _ventaService;
    public SalidasViewModel ViewModel { get; }

    public SalidasPage()
    {
        ViewModel     = App.Services.GetRequiredService<SalidasViewModel>();
        _workspace    = App.Services.GetRequiredService<IWorkspaceService>();
        _ventaService = App.Services.GetRequiredService<IVentaDocumentalService>();
        InitializeComponent();

        ViewModel.VentaOrigenSolicitada += OnVentaOrigenSolicitada;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.RefrescarAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.DetachFromContext();
        ViewModel.VentaOrigenSolicitada -= OnVentaOrigenSolicitada;
    }

    /// <inheritdoc/>
    public void ReportLiveContext() => ViewModel.ReportLiveContext();

    private async void OnVentaOrigenSolicitada(object? sender, long ventaId)
    {
        await _workspace.OpenOrActivateDocumentTabAsync(
            key:         $"venta-{ventaId}",
            title:       $"Venta #{ventaId}",
            icon:        "",
            dataLoader:  () => _ventaService.ObtenerConDetallesAsync(ventaId)
                                .ContinueWith(t => t.Result.Success ? t.Result.Value : null),
            pageFactory: dto => new VentaDocumentoPage(dto!),
            onError:     err => ViewModel.ErrorMessage = err,
            isClosable:  true);
    }
}

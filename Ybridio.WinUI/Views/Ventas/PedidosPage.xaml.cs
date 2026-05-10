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

public sealed partial class PedidosPage : Page, ILiveContextReporter
{
    private readonly IWorkspaceService _workspace;
    private readonly IPedidoService    _pedidoService;
    public PedidosViewModel ViewModel { get; }

    public PedidosPage()
    {
        ViewModel      = App.Services.GetRequiredService<PedidosViewModel>();
        _workspace     = App.Services.GetRequiredService<IWorkspaceService>();
        _pedidoService = App.Services.GetRequiredService<IPedidoService>();
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

    private void BtnNuevo_Click(object sender, RoutedEventArgs e)
    {
        _workspace.OpenTab(
            key:         $"pedido-nuevo-{Guid.NewGuid():N}",
            title:       "Nuevo Pedido",
            icon:        "",
            pageFactory: () => new PedidoDocumentoPage(null),
            isClosable:  true);
    }

    private async void BtnAbrir_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.PedidoSeleccionado is null) return;
        await AbrirPedidoEnWorkspace(ViewModel.PedidoSeleccionado.Id);
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.PedidoSeleccionado is null) return;
        _ = AbrirPedidoEnWorkspace(ViewModel.PedidoSeleccionado.Id);
    }

    private async System.Threading.Tasks.Task AbrirPedidoEnWorkspace(long id)
    {
        await _workspace.OpenOrActivateDocumentTabAsync(
            key:         $"pedido-{id}",
            title:       $"Pedido #{id}",
            icon:        "",
            dataLoader:  () => _pedidoService.ObtenerConDetallesAsync(id)
                                .ContinueWith(t => t.Result.Success ? t.Result.Value : null),
            pageFactory: dto => new PedidoDocumentoPage(dto!),
            onError:     err => ViewModel.ErrorMessage = err,
            isClosable:  true);
    }
}

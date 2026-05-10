using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Inventario;
using Ybridio.WinUI.ViewModels.Inventario;

namespace Ybridio.WinUI.Views.Inventario;

/// <summary>
/// Kardex operacional de inventario.
/// Muestra movimientos con filtros de producto, fecha, tipo y almacén.
/// El ViewModel aplica enforcement runtime de permiso <c>kardex.ver</c> y scope de almacén.
/// </summary>
public sealed partial class KardexPage : Page
{
    public KardexViewModel ViewModel { get; }

    public KardexPage()
    {
        ViewModel = App.Services.GetRequiredService<KardexViewModel>();
        InitializeComponent();

        // Navegación cruzada hacia documentos origen
        ViewModel.DocumentoOrigenSolicitado += AbrirDocumentoOrigen;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Soporte de navegación rápida desde Productos: navegar con productoId pre-filtrado
        if (e.Parameter is int productoId)
            ViewModel.BusquedaProducto = productoId.ToString();

        if (ViewModel.LoadCommand.CanExecute(null))
            await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.DetachFromContext();
        ViewModel.DocumentoOrigenSolicitado -= AbrirDocumentoOrigen;
    }

    private void AbrirDocumentoOrigen(KardexLineaDto linea)
    {
        // La navegación cruzada usa Frame.Navigate según el tipo de referencia.
        // Se puede extender aquí para abrir Venta, Entrada, Salida u OT.
        // Por ahora muestra un mensaje informativo; la integración con WorkspaceService
        // se puede completar cuando se conozca el tipo de movimiento.
        if (linea.ReferenciaId is not null && linea.Referencia is not null)
        {
            // El tipo de documento origen se infiere del nombre del movimiento.
            // Este bloque es extensible; por ahora solo navega informando el documento.
            _ = linea; // placeholder para futura navegación cruzada
        }
    }
}

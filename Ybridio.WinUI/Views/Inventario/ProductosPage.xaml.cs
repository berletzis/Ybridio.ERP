using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.WinUI.Controls.Navigation;
using Ybridio.WinUI.Helpers;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.Services.Windowing;
using Ybridio.WinUI.ViewModels.Inventario;

namespace Ybridio.WinUI.Views.Inventario;

public sealed partial class ProductosPage : Page, ILiveContextReporter
{
    private readonly IWindowManager _windowManager;
    private DataGridColumnManager? _columnManager;

    public ProductosViewModel ViewModel { get; }

    public ProductosPage()
    {
        ViewModel      = App.Services.GetRequiredService<ProductosViewModel>();
        _windowManager = App.Services.GetRequiredService<IWindowManager>();
        InitializeComponent();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.DetachFromContext();
    }

    /// <inheritdoc/>
    public void ReportLiveContext() => ViewModel.ReportLiveContext();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        ViewModel.SolicitarAbrirDetalle  = AbrirVentanaDetalle;
        ViewModel.SolicitarComparar      = AbrirVentanaComparar;
        // Sincronizar panel cuando el filtro se limpia desde el ViewModel
        ViewModel.FiltroLimpiadoCallback = () => ClasificacionPanel.ClearSelection();

        _columnManager ??= DataGridColumnManager.Initialize(
            ListaProductos, ProductosHeaderGrid, "ProductosGrid");

        if (ViewModel.LoadCommand.CanExecute(null))
            await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private void ListaProductos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.ProductosSeleccionados.Clear();
        foreach (var item in ListaProductos.SelectedItems.OfType<ProductoDto>())
            ViewModel.ProductosSeleccionados.Add(item);

        ViewModel.ProductoSeleccionado = ListaProductos.SelectedItem as ProductoDto;
        ViewModel.CompararCommand.NotifyCanExecuteChanged();
    }

    private void ListaProductos_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.ProductoSeleccionado is not null && ViewModel.EditarCommand.CanExecute(null))
            ViewModel.EditarCommand.Execute(null);
    }

    private void AbrirVentanaDetalle(ProductoDto? producto)
    {
        // WindowManager garantiza: una instancia por producto, siempre al frente,
        // centrada en la ventana principal, y limpieza automática al cerrar.
        var key = producto?.Id ?? 0;
        _windowManager.OpenWindow<ProductoDetailWindow, int>(
            key,
            () => new ProductoDetailWindow(ViewModel, producto),
            new WindowOptions
            {
                Width            = 900,
                Height           = 700,
                PositionStrategy = WindowPositionStrategy.CenterOwner
            });
    }

    private void AbrirVentanaComparar((ProductoDto A, ProductoDto B) par)
    {
        var ventana = new CompararProductosWindow(par.A, par.B);
        ventana.Activate();
    }

    // ── Toggle del panel ─────────────────────────────────────────────────────

    private void TogglePanel_Click(object sender, RoutedEventArgs e)
    {
        var panelCol  = ContentAreaGrid.ColumnDefinitions[0];
        var resizeCol = ContentAreaGrid.ColumnDefinitions[1];
        bool isOpen = panelCol.Width.Value > 0;

        if (isOpen)
        {
            panelCol.Width  = new GridLength(0);
            resizeCol.Width = new GridLength(0);
            PanelResizeHandle.Visibility = Visibility.Collapsed;
        }
        else
        {
            panelCol.Width  = new GridLength(240, GridUnitType.Pixel);
            resizeCol.Width = new GridLength(5, GridUnitType.Pixel);
            PanelResizeHandle.Visibility = Visibility.Visible;
        }
    }

    // ── Redimensionado libre del panel ───────────────────────────────────────

    private bool   _isResizing;
    private double _resizeStartX;
    private double _resizeStartWidth;

    private void PanelResize_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isResizing      = true;
        _resizeStartX    = e.GetCurrentPoint(ContentAreaGrid).Position.X;
        _resizeStartWidth = ContentAreaGrid.ColumnDefinitions[0].Width.Value;
        ((UIElement)sender).CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void PanelResize_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isResizing) return;
        var delta    = e.GetCurrentPoint(ContentAreaGrid).Position.X - _resizeStartX;
        var newWidth = Math.Clamp(_resizeStartWidth + delta, 180, 400);
        ContentAreaGrid.ColumnDefinitions[0].Width = new GridLength(newWidth, GridUnitType.Pixel);
        e.Handled = true;
    }

    private void PanelResize_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isResizing = false;
        ((UIElement)sender).ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    // ClassificationPanel → ViewModel: pasa el item completo para que el ViewModel
    // extraiga CategoriaId y Name sin tener que buscarlo en el árbol.
    private void ClasificacionPanel_SelectionChanged(object sender, ClassificationItem? e)
    {
        ViewModel.FiltrarPorClasificacion(e);
    }

    // Chip ✕: limpia filtro en ViewModel y deselecciona el nodo en el panel
    private void LimpiarFiltroChip_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.LimpiarFiltro();
        // FiltroLimpiadoCallback ya llama ClearSelection, pero llamarlo aquí garantiza
        // sincronía inmediata si el callback no estuviera asignado por algún motivo.
        ClasificacionPanel.ClearSelection();
    }
}

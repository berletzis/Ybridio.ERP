using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.WinUI.Services.Diagnostic;

namespace Ybridio.WinUI.Views.Inventario;

public sealed partial class InventarioPage : Page
{
    // Rastrear qué tabs ya fueron cargados para no recargar al volver
    private bool _existenciasLoaded;
    private bool _entradasLoaded;
    private bool _salidasLoaded;
    private bool _kardexLoaded;
    private bool _conteoLoaded;
    private bool _ordenesCompraLoaded;
    private bool _productosLoaded;

    private readonly ICurrentContextTracker _contextTracker;

    public InventarioPage()
    {
        InitializeComponent();
        _contextTracker = App.Services.GetRequiredService<ICurrentContextTracker>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // Cargar el primer tab por defecto
        LoadTab(InventarioTabs.SelectedItem as TabViewItem);
    }

    private void InventarioTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LoadTab(InventarioTabs.SelectedItem as TabViewItem);
    }

    private void LoadTab(TabViewItem? tab)
    {
        if (tab is null) return;

        // Notificar el sub-módulo activo antes de cargar/activar
        var subModule = tab.Header?.ToString() ?? "—";
        _contextTracker.SetModuleContext("Inventario", subModule);

        if (tab == TabExistencias && !_existenciasLoaded)
        {
            FrameExistencias.Navigate(typeof(ExistenciasPage));
            _existenciasLoaded = true;
        }
        else if (tab == TabEntradas && !_entradasLoaded)
        {
            FrameEntradas.Navigate(typeof(EntradasPage));
            _entradasLoaded = true;
        }
        else if (tab == TabSalidas && !_salidasLoaded)
        {
            FrameSalidas.Navigate(typeof(SalidasPage));
            _salidasLoaded = true;
        }
        else if (tab == TabKardex && !_kardexLoaded)
        {
            FrameKardex.Navigate(typeof(KardexPage));
            _kardexLoaded = true;
        }
        else if (tab == TabConteo && !_conteoLoaded)
        {
            FrameConteo.Navigate(typeof(ConteoPage));
            _conteoLoaded = true;
        }
        else if (tab == TabOrdenesCompra && !_ordenesCompraLoaded)
        {
            FrameOrdenesCompra.Navigate(typeof(OrdenesCompraPage));
            _ordenesCompraLoaded = true;
        }
        else if (tab == TabProductos && !_productosLoaded)
        {
            FrameProductos.Navigate(typeof(ProductosPage));
            _productosLoaded = true;
        }
        else
        {
            // Tab ya cargado — pedir al contenido que re-reporte su contexto vivo
            var frame = GetFrameForTab(tab);
            if (frame?.Content is ILiveContextReporter reporter)
                reporter.ReportLiveContext();
        }
    }

    private Frame? GetFrameForTab(TabViewItem tab)
    {
        if (tab == TabExistencias)    return FrameExistencias;
        if (tab == TabEntradas)       return FrameEntradas;
        if (tab == TabSalidas)        return FrameSalidas;
        if (tab == TabKardex)         return FrameKardex;
        if (tab == TabConteo)         return FrameConteo;
        if (tab == TabOrdenesCompra)  return FrameOrdenesCompra;
        if (tab == TabProductos)      return FrameProductos;
        return null;
    }
}

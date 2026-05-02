using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

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

    public InventarioPage()
    {
        InitializeComponent();
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

        // Solo navega si el frame de ese tab aún no ha cargado
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
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.WinUI.Services.Diagnostic;

namespace Ybridio.WinUI.Views.Ventas;

/// <summary>
/// Página contenedora del módulo Sales Core Operacional.
/// TabView con 4 tabs: Clientes, Cotizaciones, Pedidos, Órdenes de Trabajo.
/// Sigue el patrón lazy-load de InventarioPage/FinanzasPage.
/// </summary>
public sealed partial class VentasPage : Page
{
    private bool _clientesLoaded;
    private bool _cotizacionesLoaded;
    private bool _pedidosLoaded;
    private bool _ventasLoaded;
    private bool _otLoaded;

    private readonly ICurrentContextTracker _contextTracker;

    public VentasPage()
    {
        InitializeComponent();
        _contextTracker = App.Services.GetRequiredService<ICurrentContextTracker>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadTab(VentasTabs.SelectedItem as TabViewItem);
    }

    private void VentasTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => LoadTab(VentasTabs.SelectedItem as TabViewItem);

    private void LoadTab(TabViewItem? tab)
    {
        if (tab is null) return;

        var subModule = tab.Header?.ToString() ?? "—";
        _contextTracker.SetModuleContext("Ventas", subModule);

        if (tab == TabClientes && !_clientesLoaded)
        {
            FrameClientes.Navigate(typeof(ClientesPage));
            _clientesLoaded = true;
        }
        else if (tab == TabCotizaciones && !_cotizacionesLoaded)
        {
            FrameCotizaciones.Navigate(typeof(CotizacionesPage));
            _cotizacionesLoaded = true;
        }
        else if (tab == TabPedidos && !_pedidosLoaded)
        {
            FramePedidos.Navigate(typeof(PedidosPage));
            _pedidosLoaded = true;
        }
        else if (tab == TabVentas && !_ventasLoaded)
        {
            FrameVentas.Navigate(typeof(VentasDocumentalesPage));
            _ventasLoaded = true;
        }
        else if (tab == TabOrdenesTrabajo && !_otLoaded)
        {
            FrameOrdenesTrabajo.Navigate(typeof(OrdenesTrabajoPage));
            _otLoaded = true;
        }
        else
        {
            var frame = GetFrameForTab(tab);
            if (frame?.Content is ILiveContextReporter reporter)
                reporter.ReportLiveContext();
        }
    }

    /// <summary>
    /// Activa el tab Pedidos y abre el Pedido como Document Surface inline.
    /// Invocado desde la conversión Cotización→Pedido para mostrar el documento
    /// en el mismo contexto operacional que al abrirlo desde el grid.
    /// </summary>
    public void AbrirPedidoDesdeConversion(Ybridio.Application.DTOs.Ventas.PedidoDto pedido)
    {
        // Seleccionar tab Pedidos — dispara LoadTab sincrónico vía SelectionChanged
        VentasTabs.SelectedItem = TabPedidos;

        // Después de LoadTab, FramePedidos.Content es PedidosPage
        if (FramePedidos.Content is PedidosPage pedidosPage)
            pedidosPage.AbrirPedidoDesdeConversion(pedido);
    }

    private Frame? GetFrameForTab(TabViewItem tab)
    {
        if (tab == TabClientes)        return FrameClientes;
        if (tab == TabCotizaciones)    return FrameCotizaciones;
        if (tab == TabPedidos)         return FramePedidos;
        if (tab == TabVentas)          return FrameVentas;
        if (tab == TabOrdenesTrabajo)  return FrameOrdenesTrabajo;
        return null;
    }
}

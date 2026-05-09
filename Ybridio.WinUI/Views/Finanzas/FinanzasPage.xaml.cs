using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.WinUI.Services.Diagnostic;

namespace Ybridio.WinUI.Views.Finanzas;

/// <summary>
/// Página contenedora del módulo Finanzas Operativas.
/// TabView con 4 tabs: Gastos, Ingresos, Cuentas por Cobrar, Cuentas por Pagar.
/// Sigue el mismo patrón de lazy-load que InventarioPage.
/// </summary>
public sealed partial class FinanzasPage : Page
{
    private bool _gastosLoaded;
    private bool _ingresosLoaded;
    private bool _cxcLoaded;
    private bool _cxpLoaded;

    private readonly ICurrentContextTracker _contextTracker;

    public FinanzasPage()
    {
        InitializeComponent();
        _contextTracker = App.Services.GetRequiredService<ICurrentContextTracker>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadTab(FinanzasTabs.SelectedItem as TabViewItem);
    }

    private void FinanzasTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => LoadTab(FinanzasTabs.SelectedItem as TabViewItem);

    private void LoadTab(TabViewItem? tab)
    {
        if (tab is null) return;

        var subModule = tab.Header?.ToString() ?? "—";
        _contextTracker.SetModuleContext("Finanzas", subModule);

        if (tab == TabGastos && !_gastosLoaded)
        {
            FrameGastos.Navigate(typeof(GastosPage));
            _gastosLoaded = true;
        }
        else if (tab == TabIngresos && !_ingresosLoaded)
        {
            FrameIngresos.Navigate(typeof(IngresosPage));
            _ingresosLoaded = true;
        }
        else if (tab == TabCxC && !_cxcLoaded)
        {
            FrameCxC.Navigate(typeof(CxCPage));
            _cxcLoaded = true;
        }
        else if (tab == TabCxP && !_cxpLoaded)
        {
            FrameCxP.Navigate(typeof(CxPPage));
            _cxpLoaded = true;
        }
        else
        {
            var frame = GetFrameForTab(tab);
            if (frame?.Content is ILiveContextReporter reporter)
                reporter.ReportLiveContext();
        }
    }

    private Frame? GetFrameForTab(TabViewItem tab)
    {
        if (tab == TabGastos)   return FrameGastos;
        if (tab == TabIngresos) return FrameIngresos;
        if (tab == TabCxC)      return FrameCxC;
        if (tab == TabCxP)      return FrameCxP;
        return null;
    }
}

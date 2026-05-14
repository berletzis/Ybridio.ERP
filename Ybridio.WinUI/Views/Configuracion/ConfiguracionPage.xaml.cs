using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.WinUI.Views.Config;
using XamlApp = Microsoft.UI.Xaml.Application;
using AuditoriaViewPage = Ybridio.WinUI.Views.Config.AuditoriaPage;

namespace Ybridio.WinUI.Views.Configuracion;

/// <summary>
/// Página contenedora del módulo de Configuración Global.
///
/// Modo Global (parámetro "Global" o null):
///   NavigationView vertical institucional (Visual Studio Settings style).
///   Secciones: Empresa · Sucursales · Parámetros · Impuestos · Otros Cargos ·
///              Unidades de Medida · Tipos de Producto · Workflow · Auditoría · Seguridad
///
/// Modo Tienda (parámetro "Tienda"):
///   TabView horizontal para configuración de la sucursal activa.
/// </summary>
public sealed partial class ConfiguracionPage : Page
{
    // ── Lazy-load flags — Global NavView ─────────────────────────────────────
    private bool _empresaLoaded;
    private bool _sucursalesLoaded;
    private bool _parametrosLoaded;
    private bool _impuestosLoaded;
    private bool _otrosCargosLoaded;
    private bool _unidadesMedidaLoaded;
    private bool _tiposProductoLoaded;
    private bool _seriesDocumentoLoaded;
    private bool _workflowLoaded;
    private bool _auditoriaLoaded;
    private bool _seguridadLoaded;

    // ── Lazy-load flags — Tienda ─────────────────────────────────────────────
    private bool _usuariosTiendaLoaded;
    private bool _cajasTiendaLoaded;
    private bool _dispositivosLoaded;
    private bool _promocionesLoaded;
    private bool _almacenesLoaded;
    private bool _permisosLoaded;
    private bool _facturacionLoaded;
    private bool _personalizacionLoaded;

    public ConfiguracionPage() => InitializeComponent();

    /// <summary>
    /// Permite cambiar el modo desde el WorkspaceService (antes de añadir la página al árbol visual).
    /// </summary>
    public void SetMode(string mode)
    {
        bool esTienda = mode == "Tienda";
        NavGlobal.Visibility       = esTienda ? Visibility.Collapsed : Visibility.Visible;
        TiendaContainer.Visibility = esTienda ? Visibility.Visible   : Visibility.Collapsed;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        bool esTienda = e.Parameter is string p && p == "Tienda";

        NavGlobal.Visibility       = esTienda ? Visibility.Collapsed : Visibility.Visible;
        TiendaContainer.Visibility = esTienda ? Visibility.Visible   : Visibility.Collapsed;

        if (esTienda)
        {
            LoadTiendaTab(TabsTienda.SelectedItem as TabViewItem);
        }
        else
        {
            // Seleccionar Empresa por defecto al abrir
            if (NavGlobal.SelectedItem is null)
                NavGlobal.SelectedItem = NavGlobal.MenuItems[1]; // primer item real (post-header)
        }
    }

    // ── NavigationView Global — SelectionChanged ──────────────────────────────

    private void NavGlobal_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        var tag = item.Tag?.ToString();

        switch (tag)
        {
            case "empresa":
                LoadNavFrame(ref _empresaLoaded, typeof(EmpresaPage));
                break;
            case "sucursales":
                LoadNavFrame(ref _sucursalesLoaded, typeof(SucursalesConfigPage));
                break;
            case "parametros":
                LoadNavFrame(ref _parametrosLoaded, typeof(ParametrosPage));
                break;
            case "impuestos":
                LoadNavFrame(ref _impuestosLoaded, typeof(ImpuestosPage));
                break;
            case "otros-cargos":
                LoadNavFrame(ref _otrosCargosLoaded, typeof(OtrosCargosPage));
                break;
            case "unidades-medida":
                LoadNavFrame(ref _unidadesMedidaLoaded, typeof(UnidadesMedidaPage));
                break;
            case "tipos-producto":
                LoadNavFrame(ref _tiposProductoLoaded, typeof(TiposProductoPage));
                break;
            case "series-documento":
                LoadNavFrame(ref _seriesDocumentoLoaded, typeof(SeriesDocumentoPage));
                break;
            case "workflow":
                LoadNavFrame(ref _workflowLoaded, typeof(WorkflowPage));
                break;
            case "auditoria":
                LoadNavFrame(ref _auditoriaLoaded, typeof(AuditoriaViewPage));
                break;
            case "seguridad":
                LoadNavFrame(ref _seguridadLoaded, typeof(SeguridadGlobalPage));
                break;
        }
    }

    // ── Handlers Tienda ──────────────────────────────────────────────────────

    private void TiendaTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => LoadTiendaTab(TabsTienda.SelectedItem as TabViewItem);

    private void LoadTiendaTab(TabViewItem? tab)
    {
        if (tab is null) return;

        if (tab == TabTiendaUsuarios)
            LoadFrame(ref _usuariosTiendaLoaded, FrameUsuariosTienda, typeof(UsuariosPage));
        else if (tab == TabTiendaCajas)
            LoadPlaceholder(ref _cajasTiendaLoaded, FrameCajasTienda, "Cajas");
        else if (tab == TabTiendaDispositivos)
            LoadPlaceholder(ref _dispositivosLoaded, FrameDispositivos, "Dispositivos");
        else if (tab == TabTiendaPromociones)
            LoadPlaceholder(ref _promocionesLoaded, FramePromociones, "Promociones y Descuentos");
        else if (tab == TabTiendaAlmacenes)
            LoadPlaceholder(ref _almacenesLoaded, FrameAlmacenes, "Asignación de Almacenes");
        else if (tab == TabTiendaPermisos)
            LoadPlaceholder(ref _permisosLoaded, FramePermisos, "Permisos Especiales");
        else if (tab == TabTiendaFacturacion)
            LoadPlaceholder(ref _facturacionLoaded, FrameFacturacion, "Facturación por Tienda");
        else if (tab == TabTiendaPersonalizacion)
            LoadPlaceholder(ref _personalizacionLoaded, FramePersonalizacion, "Personalización de Tienda");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Navega el NavContentFrame al tipo de página indicado, solo la primera vez.</summary>
    private void LoadNavFrame(ref bool flag, Type pageType)
    {
        // Siempre navegar para recargar (sin lazy-load en NavigationView, para reaccionar a cambio de sección)
        NavContentFrame.Navigate(pageType);
        flag = true;
    }

    /// <summary>Navega un Frame a un tipo de página, solo la primera vez (Tienda mode).</summary>
    private static void LoadFrame(ref bool flag, Frame frame, Type pageType)
    {
        if (flag) return;
        frame.Navigate(pageType);
        flag = true;
    }

    /// <summary>Muestra un TextBlock "Próximamente" en el Frame, solo la primera vez.</summary>
    private static void LoadPlaceholder(ref bool flag, Frame frame, string nombre)
    {
        if (flag) return;
        frame.Content = new TextBlock
        {
            Text                = $"{nombre} — Próximamente",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Style               = XamlApp.Current.Resources["SubtitleTextBlockStyle"] as Style
        };
        flag = true;
    }
}

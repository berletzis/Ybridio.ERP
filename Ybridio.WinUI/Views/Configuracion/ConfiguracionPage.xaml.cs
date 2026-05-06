using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.WinUI.Views.Config;
using XamlApp = Microsoft.UI.Xaml.Application;
using AuditoriaViewPage = Ybridio.WinUI.Views.Config.AuditoriaPage;

namespace Ybridio.WinUI.Views.Configuracion;

/// <summary>
/// Página contenedora del módulo de Configuración.
/// Muestra el modo Global (Empresa, Tiendas, Seguridad) o el modo Tienda
/// (configuración específica por sucursal) según el parámetro de navegación:
/// "Global" → TabsGlobal visible | "Tienda" → TabsTienda visible.
/// </summary>
public sealed partial class ConfiguracionPage : Page
{
    // ── Lazy-load flags — Global ─────────────────────────────────────────────
    private bool _empresaLoaded;
    private bool _tiendasGlobalLoaded;
    private bool _auditoriaLoaded;
    private bool _usuariosGlobalLoaded;
    private bool _rolesGlobalLoaded;
    private bool _perfilesGlobalLoaded;

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

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        bool esTienda = e.Parameter is string p && p == "Tienda";

        TabsGlobal.Visibility = esTienda ? Visibility.Collapsed : Visibility.Visible;
        TabsTienda.Visibility = esTienda ? Visibility.Visible   : Visibility.Collapsed;

        if (esTienda)
            LoadTiendaTab(TabsTienda.SelectedItem as TabViewItem);
        else
            LoadGlobalTab(TabsGlobal.SelectedItem as TabViewItem);
    }

    // ── Handlers de selección ────────────────────────────────────────────────

    private void GlobalTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => LoadGlobalTab(TabsGlobal.SelectedItem as TabViewItem);

    private void SeguridadTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => LoadSeguridadTab(TabsSeguridad.SelectedItem as TabViewItem);

    private void TiendaTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => LoadTiendaTab(TabsTienda.SelectedItem as TabViewItem);

    // ── Carga de tabs — Global ───────────────────────────────────────────────

    private void LoadGlobalTab(TabViewItem? tab)
    {
        if (tab is null) return;

        if (tab == TabGlobalEmpresa)
            LoadFrame(ref _empresaLoaded, FrameEmpresa, typeof(EmpresaPage));
        else if (tab == TabGlobalTiendas)
            LoadFrame(ref _tiendasGlobalLoaded, FrameTiendasGlobal, typeof(SucursalesConfigPage));
        else if (tab == TabGlobalAuditoria)
            LoadFrame(ref _auditoriaLoaded, FrameAuditoria, typeof(AuditoriaViewPage));
        else if (tab == TabGlobalSeguridad)
            LoadSeguridadTab(TabsSeguridad.SelectedItem as TabViewItem);
    }

    private void LoadSeguridadTab(TabViewItem? tab)
    {
        if (tab is null) return;

        if (tab == TabUsuariosGlobal)
            LoadFrame(ref _usuariosGlobalLoaded, FrameUsuariosGlobal, typeof(UsuariosPage));
        else if (tab == TabRolesGlobal)
            LoadFrame(ref _rolesGlobalLoaded, FrameRolesGlobal, typeof(RolesPage));
        else if (tab == TabPerfilesGlobal)
            LoadPlaceholder(ref _perfilesGlobalLoaded, FramePerfilesGlobal, "Perfiles");
    }

    // ── Carga de tabs — Tienda ───────────────────────────────────────────────

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

    /// <summary>Navega el Frame al tipo de página indicado, solo la primera vez.</summary>
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

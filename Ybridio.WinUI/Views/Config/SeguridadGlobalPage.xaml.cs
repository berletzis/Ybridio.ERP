using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using XamlApp = Microsoft.UI.Xaml.Application;
using AuditoriaViewPage = Ybridio.WinUI.Views.Config.AuditoriaPage;
using ArqSegViewPage    = Ybridio.WinUI.Views.Config.ArquitecturaSegPage;

namespace Ybridio.WinUI.Views.Config;

/// <summary>
/// Página contenedora del módulo Seguridad (sub-tabs: Usuarios, Roles, Perfiles, Permisos, Scopes, Arquitectura).
/// Se carga dentro del NavigationView de ConfiguracionPage cuando el usuario selecciona "Seguridad".
/// </summary>
public sealed partial class SeguridadGlobalPage : Page
{
    private bool _usuariosLoaded;
    private bool _rolesLoaded;
    private bool _perfilesLoaded;
    private bool _permisosLoaded;
    private bool _scopesLoaded;
    private bool _arquitecturaLoaded;

    public SeguridadGlobalPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadSeguridadTab(TabsSeguridad.SelectedItem as TabViewItem);
    }

    private void SeguridadTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => LoadSeguridadTab(TabsSeguridad.SelectedItem as TabViewItem);

    private void LoadSeguridadTab(TabViewItem? tab)
    {
        if (tab is null) return;

        if (tab == TabUsuarios)
            LoadFrame(ref _usuariosLoaded, FrameUsuarios, typeof(UsuariosPage));
        else if (tab == TabRoles)
            LoadFrame(ref _rolesLoaded, FrameRoles, typeof(RolesPage));
        else if (tab == TabPerfiles)
            LoadFrame(ref _perfilesLoaded, FramePerfiles, typeof(PerfilesPage));
        else if (tab == TabPermisos)
            LoadFrame(ref _permisosLoaded, FramePermisos, typeof(PermisosPage));
        else if (tab == TabScopes)
            LoadFrame(ref _scopesLoaded, FrameScopes, typeof(ScopesPage));
        else if (tab == TabArquitectura)
            LoadFrame(ref _arquitecturaLoaded, FrameArquitectura, typeof(ArqSegViewPage));
    }

    private static void LoadFrame(ref bool flag, Frame frame, System.Type pageType)
    {
        if (flag) return;
        frame.Navigate(pageType);
        flag = true;
    }
}

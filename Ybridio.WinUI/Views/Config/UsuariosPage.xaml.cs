using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.Application.Services.Seguridad;
using Ybridio.WinUI.Services.Windowing;
using Ybridio.WinUI.ViewModels.Config;
using XamlApp = Microsoft.UI.Xaml.Application;

namespace Ybridio.WinUI.Views.Config;

public sealed partial class UsuariosPage : Page
{
    private readonly IWindowManager        _windowManager;
    private readonly ISecurityAdminService _adminService;
    private readonly IRolService           _rolService;
    private readonly IPerfilService        _perfilService;

    public UsuariosViewModel ViewModel { get; }

    public UsuariosPage()
    {
        ViewModel       = App.Services.GetRequiredService<UsuariosViewModel>();
        _windowManager  = App.Services.GetRequiredService<IWindowManager>();
        _adminService   = App.Services.GetRequiredService<ISecurityAdminService>();
        _rolService     = App.Services.GetRequiredService<IRolService>();
        _perfilService  = App.Services.GetRequiredService<IPerfilService>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.SolicitarAbrirDetalle  = AbrirDetalle;
        ViewModel.SolicitarAsignarRoles  = AbrirDialogoAsignarRoles;
        ViewModel.SolicitarAsignarPerfiles = AbrirDialogoAsignarPerfiles;
        ViewModel.SolicitarAsignarScopes = AbrirDialogoAsignarScopes;
        if (ViewModel.LoadCommand.CanExecute(null))
            await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.DetachFromContext();
    }

    // ── Detalle de usuario ────────────────────────────────────────────────────

    private void AbrirDetalle(UsuarioResumenDto? usuario)
    {
        var dto = usuario is null ? null
            : new UsuarioDto(usuario.Id, usuario.EmpresaId, usuario.Nombre, usuario.UserName, usuario.Email, usuario.Activo);

        var key = dto?.Id ?? Guid.Empty;
        _windowManager.OpenWindow<UsuarioDetailWindow, Guid>(
            key,
            () => new UsuarioDetailWindow(ViewModel, dto),
            new WindowOptions { Width = 700, Height = 600, PositionStrategy = WindowPositionStrategy.CenterOwner });
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.UsuarioSeleccionado is not null) ViewModel.EditarCommand.Execute(null);
    }

    // ── Diálogo: Asignar Roles ────────────────────────────────────────────────

    private async void AbrirDialogoAsignarRoles(UsuarioResumenDto usuario)
    {
        var todosRoles   = await _rolService.ListarAsync();
        var (roles, _)   = await ViewModel.CargarAsignacionesActualesAsync(usuario.Id);
        var rolesActSet  = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);

        var checks = new List<(CheckBox cb, RolDto rol)>();
        var panel  = new StackPanel { Spacing = 6 };

        foreach (var r in todosRoles)
        {
            var cb = new CheckBox { Content = r.Name, IsChecked = rolesActSet.Contains(r.Name) };
            checks.Add((cb, r));
            panel.Children.Add(cb);
        }

        if (checks.Count == 0)
            panel.Children.Add(new TextBlock { Text = "No hay roles disponibles.", Foreground = (Microsoft.UI.Xaml.Media.Brush)XamlApp.Current.Resources["TextFillColorSecondaryBrush"] });

        var dialog = new ContentDialog
        {
            Title               = $"Asignar Roles — {usuario.Nombre}",
            PrimaryButtonText   = "Guardar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = new ScrollViewer { Content = panel, MaxHeight = 400, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var seleccionados = checks.Where(x => x.cb.IsChecked == true).Select(x => x.rol.Name).ToList();
        await ViewModel.GuardarRolesAsync(usuario.Id, seleccionados);
    }

    // ── Diálogo: Asignar Perfiles ─────────────────────────────────────────────

    private async void AbrirDialogoAsignarPerfiles(UsuarioResumenDto usuario)
    {
        var todosPerfiles  = await _perfilService.ListarAsync();
        var (_, perfilIds) = await ViewModel.CargarAsignacionesActualesAsync(usuario.Id);
        var perfilActSet   = new HashSet<int>(perfilIds);

        var checks = new List<(CheckBox cb, PerfilDto perfil)>();
        var panel  = new StackPanel { Spacing = 6 };

        foreach (var p in todosPerfiles)
        {
            var cb = new CheckBox { Content = $"{p.Nombre}  ({p.CantidadPermisos} permisos)", IsChecked = perfilActSet.Contains(p.Id) };
            checks.Add((cb, p));
            panel.Children.Add(cb);
        }

        if (checks.Count == 0)
            panel.Children.Add(new TextBlock { Text = "No hay perfiles disponibles.", Foreground = (Microsoft.UI.Xaml.Media.Brush)XamlApp.Current.Resources["TextFillColorSecondaryBrush"] });

        var dialog = new ContentDialog
        {
            Title               = $"Asignar Perfiles — {usuario.Nombre}",
            PrimaryButtonText   = "Guardar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = new ScrollViewer { Content = panel, MaxHeight = 400, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var seleccionados = checks.Where(x => x.cb.IsChecked == true).Select(x => x.perfil.Id).ToList();
        await ViewModel.GuardarPerfilesAsync(usuario.Id, seleccionados);
    }

    // ── Diálogo: Asignar Scopes ───────────────────────────────────────────────

    private async void AbrirDialogoAsignarScopes(UsuarioResumenDto usuario)
    {
        var session     = App.Services.GetRequiredService<Ybridio.WinUI.Services.SessionService>();
        var sucDispList = await _adminService.ListarSucursalesDisponiblesAsync(session.EmpresaId);
        var almDispList = await _adminService.ListarAlmacenesDisponiblesAsync(session.EmpresaId);
        var sucActIds   = await _adminService.ObtenerSucursalesDeUsuarioAsync(usuario.Id);
        var almActIds   = await _adminService.ObtenerAlmacenesDeUsuarioAsync(usuario.Id);

        var sucActSet = new HashSet<int>(sucActIds);
        var almActSet = new HashSet<int>(almActIds);

        var checksSuc = new List<(CheckBox cb, SucursalScopeItem item)>();
        var checksAlm = new List<(CheckBox cb, AlmacenScopeItem item)>();

        var panel = new StackPanel { Spacing = 4 };

        panel.Children.Add(new TextBlock { Text = "Sucursales", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13, Margin = new Thickness(0, 0, 0, 4) });
        foreach (var s in sucDispList)
        {
            var cb = new CheckBox { Content = s.Nombre, IsChecked = sucActSet.Contains(s.Id) };
            checksSuc.Add((cb, s)); panel.Children.Add(cb);
        }
        if (sucDispList.Count == 0) panel.Children.Add(new TextBlock { Text = "Sin sucursales disponibles." });

        panel.Children.Add(new TextBlock { Text = "Almacenes", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13, Margin = new Thickness(0, 16, 0, 4) });
        foreach (var a in almDispList)
        {
            var cb = new CheckBox { Content = a.Nombre, IsChecked = almActSet.Contains(a.Id) };
            checksAlm.Add((cb, a)); panel.Children.Add(cb);
        }
        if (almDispList.Count == 0) panel.Children.Add(new TextBlock { Text = "Sin almacenes disponibles." });

        panel.Children.Add(new TextBlock { Text = "Vacío = sin restricción (accede a todo).", FontSize = 12, Margin = new Thickness(0, 16, 0, 0), Foreground = (Microsoft.UI.Xaml.Media.Brush)XamlApp.Current.Resources["TextFillColorSecondaryBrush"] });

        var dialog = new ContentDialog
        {
            Title               = $"Scopes — {usuario.Nombre}",
            PrimaryButtonText   = "Guardar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = new ScrollViewer { Content = panel, MaxHeight = 480, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            MinWidth            = 400
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var sucIds = checksSuc.Where(x => x.cb.IsChecked == true).Select(x => x.item.Id).ToList();
        var almIds = checksAlm.Where(x => x.cb.IsChecked == true).Select(x => x.item.Id).ToList();

        ViewModel.IsBusy = true;
        try
        {
            var r1 = await _adminService.AsignarSucursalesAUsuarioAsync(usuario.Id, sucIds);
            if (!r1.Success) { ViewModel.ErrorMessage = r1.Error ?? "Error al guardar sucursales."; return; }
            var r2 = await _adminService.AsignarAlmacenesAUsuarioAsync(usuario.Id, almIds);
            if (!r2.Success) { ViewModel.ErrorMessage = r2.Error ?? "Error al guardar almacenes."; return; }
            ViewModel.SuccessMessage = "Scopes actualizados.";
            await ViewModel.LoadCommand.ExecuteAsync(null);
        }
        finally { ViewModel.IsBusy = false; }
    }
}

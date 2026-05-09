using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.Application.Services.Seguridad;
using Ybridio.WinUI.ViewModels.Config;
using XamlApp = Microsoft.UI.Xaml.Application;

namespace Ybridio.WinUI.Views.Config;

public sealed partial class RolesPage : Page
{
    private readonly ISecurityAdminService _adminService;
    public RolesViewModel ViewModel { get; }

    public RolesPage()
    {
        ViewModel     = App.Services.GetRequiredService<RolesViewModel>();
        _adminService = App.Services.GetRequiredService<ISecurityAdminService>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.SolicitarAsignarPermisos = AbrirDialogoAsignarPermisos;
        if (ViewModel.LoadCommand.CanExecute(null))
            await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.DetachFromContext();
    }

    // ── Diálogo: Nuevo Rol ────────────────────────────────────────────────────

    private async void BtnNuevo_Click(object sender, RoutedEventArgs e)
    {
        var txt = new TextBox { Header = "Nombre del rol *", PlaceholderText = "Ej: Administrador" };
        var dialog = new ContentDialog
        {
            Title             = "Nuevo Rol",
            PrimaryButtonText = "Crear",
            CloseButtonText   = "Cancelar",
            DefaultButton     = ContentDialogButton.Primary,
            Content           = txt,
            XamlRoot          = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(txt.Text))
        {
            var result = await ViewModel.RolService.CrearAsync(txt.Text);
            if (result.Success)
            {
                ViewModel.SuccessMessage = $"Rol '{result.Value?.Name}' creado.";
                await ViewModel.LoadCommand.ExecuteAsync(null);
            }
            else { ViewModel.ErrorMessage = result.Error ?? "No se pudo crear el rol."; }
        }
    }

    // ── Diálogo: Asignar Permisos a Rol ──────────────────────────────────────

    private async void AbrirDialogoAsignarPermisos(RolAdminDto rol)
    {
        var todosPermisos    = await _adminService.ListarPermisosAsync();
        var permisosActuales = await ViewModel.CargarPermisosDeRolAsync(rol.Id);
        var permisoActSet    = new HashSet<int>(permisosActuales);

        var checks = new List<(CheckBox cb, PermisoAdminDto permiso)>();
        var panel  = new StackPanel { Spacing = 4 };
        string? moduloActual = null;

        foreach (var p in todosPermisos)
        {
            if (p.ModuloNombre != moduloActual)
            {
                moduloActual = p.ModuloNombre;
                panel.Children.Add(new TextBlock
                {
                    Text       = moduloActual,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)XamlApp.Current.Resources["AccentFillColorDefaultBrush"],
                    Margin     = new Thickness(0, moduloActual != todosPermisos[0].ModuloNombre ? 10 : 0, 0, 2)
                });
            }
            var cb = new CheckBox
            {
                Content   = $"{p.Clave}  —  {p.Nombre}",
                IsChecked = permisoActSet.Contains(p.Id)
            };
            checks.Add((cb, p));
            panel.Children.Add(cb);
        }

        var dialog = new ContentDialog
        {
            Title               = $"Permisos — {rol.Nombre}",
            PrimaryButtonText   = "Guardar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = new ScrollViewer { Content = panel, MaxHeight = 480, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            MinWidth            = 600
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var seleccionados = checks.Where(x => x.cb.IsChecked == true).Select(x => x.permiso.Id).ToList();
        await ViewModel.GuardarPermisosRolAsync(rol.Id, seleccionados);
    }
}

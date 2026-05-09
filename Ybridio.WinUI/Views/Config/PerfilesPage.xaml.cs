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
using Ybridio.WinUI.ViewModels.Config;
using XamlApp = Microsoft.UI.Xaml.Application;

namespace Ybridio.WinUI.Views.Config;

public sealed partial class PerfilesPage : Page
{
    private readonly ISecurityAdminService _adminService;
    public PerfilesViewModel ViewModel { get; }

    public PerfilesPage()
    {
        ViewModel     = App.Services.GetRequiredService<PerfilesViewModel>();
        _adminService = App.Services.GetRequiredService<ISecurityAdminService>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.SolicitarNuevoEditar           = AbrirDialogoNuevoEditar;
        ViewModel.SolicitarAdministrarPermisos    = AbrirDialogoAdministrarPermisos;
        if (ViewModel.LoadCommand.CanExecute(null))
            await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.DetachFromContext();
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.PerfilSeleccionado is not null) ViewModel.EditarCommand.Execute(null);
    }

    // ── Diálogo Nuevo / Editar ────────────────────────────────────────────────

    private async void AbrirDialogoNuevoEditar(PerfilDto? perfil)
    {
        var txtNombre      = new TextBox { PlaceholderText = "Nombre del perfil", Text = perfil?.Nombre ?? string.Empty };
        var txtDescripcion = new TextBox { PlaceholderText = "Descripción (opcional)", Text = perfil?.Descripcion ?? string.Empty };
        var chkActivo      = new CheckBox { Content = "Activo", IsChecked = perfil?.Activo ?? true };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "Nombre *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(txtNombre);
        panel.Children.Add(new TextBlock { Text = "Descripción", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(txtDescripcion);
        if (perfil is not null) panel.Children.Add(chkActivo);

        var dialog = new ContentDialog
        {
            Title               = perfil is null ? "Nuevo Perfil" : "Editar Perfil",
            PrimaryButtonText   = "Guardar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = panel
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var nombre      = txtNombre.Text.Trim();
        var descripcion = string.IsNullOrWhiteSpace(txtDescripcion.Text) ? null : txtDescripcion.Text.Trim();
        var activo      = chkActivo.IsChecked ?? true;

        if (string.IsNullOrEmpty(nombre))
        {
            ViewModel.ErrorMessage = "El nombre del perfil es obligatorio.";
            return;
        }

        await ViewModel.GuardarPerfilAsync(perfil, nombre, descripcion, activo);
    }

    // ── Diálogo Administrar Permisos ──────────────────────────────────────────

    private async void AbrirDialogoAdministrarPermisos(PerfilDto perfil)
    {
        var todosPermisos    = await _adminService.ListarPermisosAsync();
        var permisosActuales = await _adminService.ObtenerPermisosDePerfilAsync(perfil.Id);
        var perfilIds        = new HashSet<int>(permisosActuales);

        var checkboxes = new List<(CheckBox cb, PermisoAdminDto permiso)>();
        var panel      = new StackPanel { Spacing = 4 };
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
                    Margin     = new Thickness(0, moduloActual != todosPermisos[0].ModuloNombre ? 8 : 0, 0, 4),
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)XamlApp.Current.Resources["AccentFillColorDefaultBrush"]
                });
            }
            var cb = new CheckBox
            {
                Content   = $"{p.Clave}  —  {p.Nombre}",
                IsChecked = perfilIds.Contains(p.Id)
            };
            checkboxes.Add((cb, p));
            panel.Children.Add(cb);
        }

        var scroll = new ScrollViewer { Content = panel, MaxHeight = 480, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        var dialog = new ContentDialog
        {
            Title               = $"Permisos — {perfil.Nombre}",
            PrimaryButtonText   = "Guardar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = scroll,
            MinWidth            = 600
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var seleccionados = checkboxes
            .Where(x => x.cb.IsChecked == true)
            .Select(x => x.permiso.Id)
            .ToList();

        await ViewModel.GuardarPermisosPerfilAsync(perfil.Id, seleccionados);
    }
}

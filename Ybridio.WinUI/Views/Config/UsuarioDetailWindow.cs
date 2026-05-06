using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;
using Ybridio.Application.DTOs.Core;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.WinUI.ViewModels.Config;
using XamlApp = Microsoft.UI.Xaml.Application;

namespace Ybridio.WinUI.Views.Config;

public sealed class UsuarioDetailWindow : Microsoft.UI.Xaml.Window
{
    private readonly UsuariosViewModel _vm;
    private readonly UsuarioDto?       _original;

    private TextBox  _txtNombre   = null!;
    private TextBox  _txtEmail    = null!;
    private TextBox  _txtUserName = null!;
    private PasswordBox _pbPassword = null!;
    private CheckBox _chkActivo   = null!;
    private Button   _btnGuardar  = null!;
    private TextBlock _txtError   = null!;

    // Sucursales + roles: checkboxes dinámicos
    private readonly Dictionary<int,    CheckBox> _sucursalChecks = new();
    private readonly Dictionary<string, CheckBox> _rolChecks      = new();

    private IReadOnlyList<SucursalDto> _todasSucursales  = [];
    private IReadOnlyList<string>      _todosRoles        = [];
    private IReadOnlyList<int>         _sucursalesActuales = [];
    private IReadOnlyList<string>      _rolesActuales      = [];

    public UsuarioDetailWindow(UsuariosViewModel vm, UsuarioDto? usuario)
    {
        _vm       = vm;
        _original = usuario;
        Title     = usuario is null ? "Nuevo usuario" : $"Editar: {usuario.Nombre}";

        try
        {
            var main = App.Services.GetRequiredService<MainWindow>();
            main.Closed += (_, _) => Close();
        }
        catch { }

        Content = BuildUI();
        _ = LoadRelatedDataAsync();
    }

    private UIElement BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header
        var header = new TextBlock
        {
            Text   = _original is null ? "Nuevo usuario" : "Editar usuario",
            Style  = XamlApp.Current.Resources["SubtitleTextBlockStyle"] as Style,
            Margin = new Thickness(20, 16, 20, 8)
        };
        root.Children.Add(header);
        Grid.SetRow(header, 0);

        // Form
        var scroll = new ScrollViewer { Padding = new Thickness(20, 0, 20, 8) };
        var form   = new StackPanel { Spacing = 16 };

        // ── Datos básicos ──────────────────────────────────────────────────────
        form.Children.Add(SectionHeader("Datos básicos"));

        _txtNombre   = new TextBox { Header = "Nombre *",        PlaceholderText = "Nombre completo" };
        _txtEmail    = new TextBox { Header = "Email *",         PlaceholderText = "usuario@dominio.com" };
        _txtUserName = new TextBox { Header = "Nombre de usuario", PlaceholderText = "usuario" };
        _pbPassword  = new PasswordBox { Header = _original is null ? "Contraseña *" : "Nueva contraseña (dejar vacío para no cambiar)" };
        _chkActivo   = new CheckBox { Content = "Activo", IsChecked = true };

        form.Children.Add(TwoColRow(_txtNombre, _txtEmail));
        form.Children.Add(TwoColRow(_txtUserName, _chkActivo));
        if (_original is null) form.Children.Add(_pbPassword);

        // ── Sucursales ─────────────────────────────────────────────────────────
        form.Children.Add(SectionHeader("Sucursales asignadas"));
        var tiendaScrollContent = new StackPanel { Spacing = 6 };
        tiendaScrollContent.Tag = "sucursales";
        var tiendaScroll = new ScrollViewer
        {
            Content = tiendaScrollContent,
            MaxHeight = 160,
            VerticalScrollMode = ScrollMode.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        form.Children.Add(tiendaScroll);

        // ── Roles ──────────────────────────────────────────────────────────────
        form.Children.Add(SectionHeader("Roles asignados"));
        var rolScrollContent = new StackPanel { Spacing = 6 };
        rolScrollContent.Tag = "roles";
        var rolScroll = new ScrollViewer
        {
            Content = rolScrollContent,
            MaxHeight = 160,
            VerticalScrollMode = ScrollMode.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        form.Children.Add(rolScroll);

        // Error
        _txtError = new TextBlock
        {
            Foreground   = new SolidColorBrush(Colors.Red),
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 4, 0, 0)
        };
        form.Children.Add(_txtError);

        scroll.Content = form;
        root.Children.Add(scroll);
        Grid.SetRow(scroll, 1);

        // Buttons
        var btnRow = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Padding = new Thickness(20, 8, 20, 16)
        };
        var btnCancelar = new Button { Content = "Cancelar" };
        btnCancelar.Click += (_, _) => Close();

        _btnGuardar = new Button
        {
            Content = "Guardar",
            Style   = XamlApp.Current.Resources["AccentButtonStyle"] as Style
        };
        _btnGuardar.Click += BtnGuardar_Click;

        btnRow.Children.Add(btnCancelar);
        btnRow.Children.Add(_btnGuardar);
        root.Children.Add(btnRow);
        Grid.SetRow(btnRow, 2);

        // Keep references to check panels
        _tiendaPanel = tiendaScrollContent;
        _rolPanel    = rolScrollContent;

        return root;
    }

    private StackPanel _tiendaPanel = null!;
    private StackPanel _rolPanel    = null!;

    private async Task LoadRelatedDataAsync()
    {
        try
        {
            _todasSucursales = await _vm.Service.ListarSucursalesAsync(
                _vm.Session.Usuario?.Id ?? Guid.Empty);
            // For edit mode, load currently assigned sucursales/roles
            if (_original is not null)
            {
                _sucursalesActuales = (await _vm.Service.ListarSucursalesAsync(_original.Id))
                    .Select(t => t.Id).ToList();
                _rolesActuales   = await _vm.Service.ListarRolesAsync(_original.Id);
            }

            // Usar servicio de roles a través del ViewModel
            _todosRoles = (await App.Services
                .GetRequiredService<Application.Services.Seguridad.IRolService>()
                .ListarAsync())
                .Select(r => r.Name)
                .ToList();

            BuildCheckBoxes();
            if (_original is not null) PopulateForm(_original);
        }
        catch { }
    }

    private void BuildCheckBoxes()
    {
        _tiendaPanel.Children.Clear();
        _sucursalChecks.Clear();
        foreach (var t in _todasSucursales)
        {
            var cb = new CheckBox
            {
                Content   = t.Nombre,
                IsChecked = _sucursalesActuales.Contains(t.Id)
            };
            _sucursalChecks[t.Id] = cb;
            _tiendaPanel.Children.Add(cb);
        }

        _rolPanel.Children.Clear();
        _rolChecks.Clear();
        foreach (var r in _todosRoles)
        {
            var cb = new CheckBox
            {
                Content   = r,
                IsChecked = _rolesActuales.Contains(r)
            };
            _rolChecks[r] = cb;
            _rolPanel.Children.Add(cb);
        }
    }

    private void PopulateForm(UsuarioDto u)
    {
        _txtNombre.Text   = u.Nombre;
        _txtEmail.Text    = u.Email ?? string.Empty;
        _txtUserName.Text = u.UserName;
        _chkActivo.IsChecked = u.Activo;
    }

    private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        _txtError.Text = string.Empty;
        var nombre   = _txtNombre.Text.Trim();
        var email    = _txtEmail.Text.Trim();
        var userName = _txtUserName.Text.Trim();

        if (string.IsNullOrWhiteSpace(nombre)) { _txtError.Text = "El nombre es obligatorio."; return; }
        if (string.IsNullOrWhiteSpace(email))  { _txtError.Text = "El email es obligatorio."; return; }

        _btnGuardar.IsEnabled = false;
        try
        {
            var tiendaIds = _sucursalChecks
                .Where(kv => kv.Value.IsChecked == true)
                .Select(kv => kv.Key)
                .ToList();
            var roles = _rolChecks
                .Where(kv => kv.Value.IsChecked == true)
                .Select(kv => kv.Key)
                .ToList();

            if (_original is null)
            {
                var pw = _pbPassword.Password;
                if (string.IsNullOrWhiteSpace(pw)) { _txtError.Text = "La contraseña es obligatoria."; return; }

                var dto    = new CrearUsuarioDto(_vm.Session.EmpresaId, nombre,
                    string.IsNullOrWhiteSpace(userName) ? email : userName, email, pw);
                var result = await _vm.Service.CrearAsync(dto);
                if (!result.Success) { _txtError.Text = result.Error ?? "No se pudo crear."; return; }

                var newId = result.Value!.Id;
                await _vm.Service.AsignarSucursalesAsync(newId, tiendaIds);
                await _vm.Service.AsignarRolesAsync(newId, roles);
            }
            else
            {
                var dto    = new ActualizarUsuarioDto(nombre, email, _chkActivo.IsChecked ?? true);
                var result = await _vm.Service.ActualizarAsync(_original.Id, dto, _vm.Session.Usuario!.Id);
                if (!result.Success) { _txtError.Text = result.Error ?? "No se pudo actualizar."; return; }

                await _vm.Service.AsignarSucursalesAsync(_original.Id, tiendaIds);
                await _vm.Service.AsignarRolesAsync(_original.Id, roles);
            }

            await _vm.LoadAsync();
            Close();
        }
        catch (Exception ex) { _txtError.Text = ex.Message; }
        finally { _btnGuardar.IsEnabled = true; }
    }

    // ── Layout helpers ────────────────────────────────────────────────────────

    private static TextBlock SectionHeader(string title) => new()
    {
        Text   = title,
        Style  = XamlApp.Current.Resources["BodyStrongTextBlockStyle"] as Style,
        Margin = new Thickness(0, 8, 0, 0)
    };

    private static Grid TwoColRow(FrameworkElement left, FrameworkElement right)
    {
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.Children.Add(left);  Grid.SetColumn(left, 0);
        g.Children.Add(right); Grid.SetColumn(right, 1);
        return g;
    }
}

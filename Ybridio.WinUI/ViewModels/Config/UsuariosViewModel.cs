using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.Application.Services.Seguridad;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.ViewModels.Config;

/// <summary>
/// ViewModel para la administración de usuarios del sistema.
/// Muestra usuarios con roles y perfiles asignados. Soporta asignación de roles,
/// perfiles y scopes desde callbacks que abren diálogos en la Page.
/// </summary>
public sealed partial class UsuariosViewModel : BaseContextViewModel
{
    private readonly IUsuarioService       _service;
    private readonly ISecurityAdminService _adminService;

    public ObservableCollection<UsuarioResumenDto> Usuarios { get; } = [];

    [ObservableProperty] private UsuarioResumenDto? usuarioSeleccionado;
    [ObservableProperty] private string             busqueda       = string.Empty;
    [ObservableProperty] private bool               isBusy;
    [ObservableProperty] private string             errorMessage   = string.Empty;
    [ObservableProperty] private string             successMessage = string.Empty;

    private IReadOnlyList<UsuarioResumenDto> _todos = [];

    // Callbacks que requieren XamlRoot — asignados por la Page
    public Action<UsuarioResumenDto?>? SolicitarAbrirDetalle;
    public Action<UsuarioResumenDto>?  SolicitarAsignarRoles;
    public Action<UsuarioResumenDto>?  SolicitarAsignarPerfiles;
    public Action<UsuarioResumenDto>?  SolicitarAsignarScopes;

    public UsuariosViewModel(
        IUsuarioService       service,
        ISecurityAdminService adminService,
        SessionService        session) : base(session)
    {
        _service      = service;
        _adminService = adminService;
    }

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy       = true;
        ErrorMessage = string.Empty;
        try
        {
            _todos = await _adminService.ListarUsuariosConDetalleAsync(Session.EmpresaId, ct);
            AplicarFiltro();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Nuevo() => SolicitarAbrirDetalle?.Invoke(null);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private void Editar() => SolicitarAbrirDetalle?.Invoke(UsuarioSeleccionado);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task CambiarActivoAsync(CancellationToken ct = default)
    {
        if (UsuarioSeleccionado is null || Session.Usuario is null) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var nuevo  = !UsuarioSeleccionado.Activo;
            var result = await _service.CambiarActivoAsync(UsuarioSeleccionado.Id, nuevo, Session.Usuario.Id, ct);
            if (result.Success) { SuccessMessage = nuevo ? "Activado." : "Desactivado."; await LoadAsync(ct); }
            else                { ErrorMessage   = result.Error ?? "No se pudo cambiar el estado."; }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private void AsignarRoles() => SolicitarAsignarRoles?.Invoke(UsuarioSeleccionado!);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private void AsignarPerfiles() => SolicitarAsignarPerfiles?.Invoke(UsuarioSeleccionado!);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private void AsignarScopes() => SolicitarAsignarScopes?.Invoke(UsuarioSeleccionado!);

    // ── Operaciones de asignación (llamadas desde diálogos en la Page) ─────────

    /// <summary>Guarda los roles asignados a un usuario.</summary>
    public async Task<bool> GuardarRolesAsync(
        Guid usuarioId, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var result = await _service.AsignarRolesAsync(usuarioId, roles, ct);
            if (!result.Success) { ErrorMessage = result.Error ?? "No se pudieron guardar los roles."; return false; }
            SuccessMessage = "Roles actualizados.";
            await LoadAsync(ct);
            return true;
        }
        finally { IsBusy = false; }
    }

    /// <summary>Guarda los perfiles asignados a un usuario.</summary>
    public async Task<bool> GuardarPerfilesAsync(
        Guid usuarioId, IReadOnlyList<int> perfilIds, CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var result = await _adminService.AsignarPerfilesAUsuarioAsync(usuarioId, perfilIds, ct);
            if (!result.Success) { ErrorMessage = result.Error ?? "No se pudieron guardar los perfiles."; return false; }
            SuccessMessage = "Perfiles actualizados.";
            await LoadAsync(ct);
            return true;
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Obtiene los roles actuales de un usuario como lista de nombres,
    /// y los IDs de perfiles asignados.
    /// </summary>
    public async Task<(IReadOnlyList<string> Roles, IReadOnlyList<int> PerfilIds)>
        CargarAsignacionesActualesAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var roles    = await _service.ListarRolesAsync(usuarioId, ct);
        var perfilIds = await _adminService.ObtenerPerfilesDeUsuarioAsync(usuarioId, ct);
        return (roles, perfilIds);
    }

    // Expone el service para uso desde la Page (acceso a UsuarioDto para ventana de detalle)
    public IUsuarioService Service => _service;

    private bool HaySeleccion() => UsuarioSeleccionado is not null;

    partial void OnUsuarioSeleccionadoChanged(UsuarioResumenDto? value)
    {
        EditarCommand.NotifyCanExecuteChanged();
        CambiarActivoCommand.NotifyCanExecuteChanged();
        AsignarRolesCommand.NotifyCanExecuteChanged();
        AsignarPerfilesCommand.NotifyCanExecuteChanged();
        AsignarScopesCommand.NotifyCanExecuteChanged();
    }

    partial void OnBusquedaChanged(string value) => AplicarFiltro();

    private void AplicarFiltro()
    {
        Usuarios.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t)
            ? _todos
            : _todos.Where(u =>
                u.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                (u.Email?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false));
        foreach (var u in lista) Usuarios.Add(u);
    }

    protected override Task OnContextChangedAsync() => LoadAsync();
}

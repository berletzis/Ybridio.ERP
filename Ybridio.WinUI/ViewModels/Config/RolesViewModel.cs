using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.Application.Services.Seguridad;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.ViewModels.Config;

/// <summary>
/// ViewModel para la administración de roles del sistema.
/// Muestra roles con conteo de permisos y usuarios. Soporta asignación de permisos por rol.
/// </summary>
public sealed partial class RolesViewModel : BaseContextViewModel
{
    private readonly IRolService           _rolService;
    private readonly ISecurityAdminService _adminService;

    public ObservableCollection<RolAdminDto> Roles { get; } = [];

    [ObservableProperty] private RolAdminDto? rolSeleccionado;
    [ObservableProperty] private bool         isBusy;
    [ObservableProperty] private string       errorMessage   = string.Empty;
    [ObservableProperty] private string       successMessage = string.Empty;

    // Callback para abrir diálogo de asignación de permisos (requiere XamlRoot desde la Page)
    public Action<RolAdminDto>? SolicitarAsignarPermisos;

    public RolesViewModel(
        IRolService           rolService,
        ISecurityAdminService adminService,
        SessionService        session) : base(session)
    {
        _rolService   = rolService;
        _adminService = adminService;
    }

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy       = true;
        ErrorMessage = string.Empty;
        try
        {
            var lista = await _adminService.ListarRolesConDetalleAsync(ct);
            Roles.Clear();
            foreach (var r in lista) Roles.Add(r);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (RolSeleccionado is null) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var result = await _rolService.EliminarAsync(RolSeleccionado.Id, ct);
            if (result.Success)
            {
                Roles.Remove(RolSeleccionado);
                RolSeleccionado = null;
                SuccessMessage  = "Rol eliminado.";
            }
            else { ErrorMessage = result.Error ?? "No se pudo eliminar el rol."; }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private void AsignarPermisos() => SolicitarAsignarPermisos?.Invoke(RolSeleccionado!);

    /// <summary>Carga los IDs de permisos actualmente asignados a un rol.</summary>
    public Task<IReadOnlyList<int>> CargarPermisosDeRolAsync(Guid rolId, CancellationToken ct = default)
        => _adminService.ObtenerPermisosDeRolAsync(rolId, ct);

    /// <summary>Guarda los permisos asignados a un rol.</summary>
    public async Task<bool> GuardarPermisosRolAsync(
        Guid rolId, IReadOnlyList<int> permisoIds, CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var result = await _adminService.AsignarPermisosARolAsync(rolId, permisoIds, ct);
            if (!result.Success) { ErrorMessage = result.Error ?? "No se pudieron guardar los permisos."; return false; }
            SuccessMessage = "Permisos actualizados.";
            await LoadAsync(ct);
            return true;
        }
        finally { IsBusy = false; }
    }

    public IRolService RolService => _rolService;

    private bool HaySeleccion() => RolSeleccionado is not null;

    partial void OnRolSeleccionadoChanged(RolAdminDto? value)
    {
        EliminarCommand.NotifyCanExecuteChanged();
        AsignarPermisosCommand.NotifyCanExecuteChanged();
    }

    // Roles son globales — no cambian con sucursal activa
    protected override Task OnContextChangedAsync() => Task.CompletedTask;
}

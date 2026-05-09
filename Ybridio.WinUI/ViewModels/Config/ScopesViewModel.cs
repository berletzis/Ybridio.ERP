using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.Application.Services.Seguridad;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.ViewModels.Config;

/// <summary>
/// ViewModel para la administración de scopes de seguridad por usuario.
/// Permite visualizar y asignar sucursales y almacenes a cada usuario.
/// </summary>
public sealed partial class ScopesViewModel : BaseContextViewModel
{
    private readonly ISecurityAdminService _adminService;

    public ObservableCollection<ScopeUsuarioDto> Scopes { get; } = [];

    [ObservableProperty] private ScopeUsuarioDto? scopeSeleccionado;
    [ObservableProperty] private string           busqueda       = string.Empty;
    [ObservableProperty] private bool             isBusy;
    [ObservableProperty] private string           errorMessage   = string.Empty;
    [ObservableProperty] private string           successMessage = string.Empty;

    private IReadOnlyList<ScopeUsuarioDto> _todos = [];

    // Callback para abrir diálogo de asignación de scopes (requiere XamlRoot desde la Page)
    public Action<ScopeUsuarioDto>? SolicitarEditarScopes;

    public ScopesViewModel(ISecurityAdminService adminService, SessionService session) : base(session)
        => _adminService = adminService;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy       = true;
        ErrorMessage = string.Empty;
        try
        {
            _todos = await _adminService.ListarScopesUsuariosAsync(Session.EmpresaId, ct);
            AplicarFiltro();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private void EditarScopes() => SolicitarEditarScopes?.Invoke(ScopeSeleccionado!);

    /// <summary>Carga datos necesarios para el diálogo de asignación: sucursales y almacenes disponibles.</summary>
    public async Task<(IReadOnlyList<SucursalScopeItem> Sucursales, IReadOnlyList<AlmacenScopeItem> Almacenes)>
        CargarDisponiblesAsync(CancellationToken ct = default)
    {
        var suc = await _adminService.ListarSucursalesDisponiblesAsync(Session.EmpresaId, ct);
        var alm = await _adminService.ListarAlmacenesDisponiblesAsync(Session.EmpresaId, ct);
        return (suc, alm);
    }

    /// <summary>Carga los scopes actuales de un usuario (IDs).</summary>
    public async Task<(IReadOnlyList<int> SucursalIds, IReadOnlyList<int> AlmacenIds)>
        CargarScopesActualesAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var suc = await _adminService.ObtenerSucursalesDeUsuarioAsync(usuarioId, ct);
        var alm = await _adminService.ObtenerAlmacenesDeUsuarioAsync(usuarioId, ct);
        return (suc, alm);
    }

    /// <summary>Guarda los scopes de sucursales y almacenes para un usuario.</summary>
    public async Task<bool> GuardarScopesAsync(
        Guid usuarioId,
        IReadOnlyList<int> sucursalIds,
        IReadOnlyList<int> almacenIds,
        CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r1 = await _adminService.AsignarSucursalesAUsuarioAsync(usuarioId, sucursalIds, ct);
            if (!r1.Success) { ErrorMessage = r1.Error ?? "Error al guardar sucursales."; return false; }

            var r2 = await _adminService.AsignarAlmacenesAUsuarioAsync(usuarioId, almacenIds, ct);
            if (!r2.Success) { ErrorMessage = r2.Error ?? "Error al guardar almacenes."; return false; }

            SuccessMessage = "Scopes actualizados.";
            await LoadAsync(ct);
            return true;
        }
        finally { IsBusy = false; }
    }

    private bool HaySeleccion() => ScopeSeleccionado is not null;

    partial void OnScopeSeleccionadoChanged(ScopeUsuarioDto? value)
        => EditarScopesCommand.NotifyCanExecuteChanged();

    partial void OnBusquedaChanged(string value) => AplicarFiltro();

    private void AplicarFiltro()
    {
        Scopes.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t)
            ? _todos
            : _todos.Where(s => s.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase));
        foreach (var s in lista) Scopes.Add(s);
    }

    protected override Task OnContextChangedAsync() => LoadAsync();
}

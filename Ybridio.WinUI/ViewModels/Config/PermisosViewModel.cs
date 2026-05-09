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
/// ViewModel para la vista de permisos del sistema (solo lectura).
/// Muestra todos los permisos organizados por módulo con capacidad de búsqueda.
/// </summary>
public sealed partial class PermisosViewModel : BaseContextViewModel
{
    private readonly ISecurityAdminService _adminService;

    public ObservableCollection<PermisoAdminDto> Permisos { get; } = [];

    [ObservableProperty] private PermisoAdminDto? permisoSeleccionado;
    [ObservableProperty] private string           busqueda     = string.Empty;
    [ObservableProperty] private bool             isBusy;
    [ObservableProperty] private string           errorMessage = string.Empty;

    private IReadOnlyList<PermisoAdminDto> _todos = [];

    public PermisosViewModel(ISecurityAdminService adminService, SessionService session) : base(session)
        => _adminService = adminService;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy       = true;
        ErrorMessage = string.Empty;
        try
        {
            _todos = await _adminService.ListarPermisosAsync(ct);
            AplicarFiltro();
        }
        finally { IsBusy = false; }
    }

    partial void OnBusquedaChanged(string value) => AplicarFiltro();

    private void AplicarFiltro()
    {
        Permisos.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t)
            ? _todos
            : _todos.Where(p =>
                p.Clave.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                p.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                p.ModuloNombre.Contains(t, StringComparison.OrdinalIgnoreCase));
        foreach (var p in lista) Permisos.Add(p);
    }

    protected override Task OnContextChangedAsync() => Task.CompletedTask;
}

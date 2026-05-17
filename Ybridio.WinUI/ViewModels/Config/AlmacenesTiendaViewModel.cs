using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Inventario;
using Ybridio.Application.Services.Inventario;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.ViewModels.Config;

/// <summary>
/// ViewModel para el tab Almacenes en Configuración Sucursal.
/// Gestiona los almacenes de la sucursal activa en sesión.
/// Patrón: Singleton Operational Surface (ADR-050) — grid + CommandBar.
/// </summary>
public sealed partial class AlmacenesTiendaViewModel : BaseContextViewModel
{
    private readonly IAlmacenService _service;

    public ObservableCollection<AlmacenDto> Almacenes { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    [NotifyCanExecuteChangedFor(nameof(MarcarPrincipalCommand))]
    [NotifyCanExecuteChangedFor(nameof(CambiarActivoCommand))]
    [NotifyCanExecuteChangedFor(nameof(EliminarCommand))]
    private AlmacenDto? almacenSeleccionado;

    [ObservableProperty] private bool   isBusy;
    [ObservableProperty] private string errorMessage   = string.Empty;
    [ObservableProperty] private string successMessage = string.Empty;
    [ObservableProperty] private string busqueda       = string.Empty;

    private System.Collections.Generic.IReadOnlyList<AlmacenDto> _todos =
        System.Array.Empty<AlmacenDto>();

    /// <summary>Nombre de la sucursal activa — para mostrar en el header/status bar.</summary>
    public string SucursalNombre => Session.SucursalNombre;

    /// <summary>Callback asignado por la Page para abrir diálogo Nuevo/Editar (requiere XamlRoot).</summary>
    public Action<AlmacenDto?>? SolicitarNuevoEditar;

    public AlmacenesTiendaViewModel(IAlmacenService service, SessionService session)
        : base(session) => _service = service;

    protected override Task OnContextChangedAsync() => LoadAsync();

    partial void OnBusquedaChanged(string _) => AplicarFiltro();

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            _todos = await _service.ListarPorSucursalAsync(Session.SucursalId, ct);
            AplicarFiltro();
        }
        catch (TaskCanceledException) { }
        finally { IsBusy = false; }
    }

    private void AplicarFiltro()
    {
        Almacenes.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t)
            ? _todos
            : System.Linq.Enumerable.Where(_todos, a =>
                a.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                (a.Codigo?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false));
        foreach (var a in lista) Almacenes.Add(a);
    }

    [RelayCommand]
    private void Nuevo() => SolicitarNuevoEditar?.Invoke(null);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private void Editar() => SolicitarNuevoEditar?.Invoke(AlmacenSeleccionado);

    [RelayCommand(CanExecute = nameof(PuedeMarcarPrincipal))]
    private async Task MarcarPrincipalAsync(CancellationToken ct = default)
    {
        if (AlmacenSeleccionado is null || Session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.MarcarPrincipalAsync(
                AlmacenSeleccionado.Id, Session.Usuario.Id, ct);
            if (r.Success) { SuccessMessage = "Almacén marcado como principal."; await LoadAsync(ct); }
            else           { ErrorMessage   = r.Error ?? "No se pudo marcar como principal."; }
        }
        catch (TaskCanceledException) { }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task CambiarActivoAsync(CancellationToken ct = default)
    {
        if (AlmacenSeleccionado is null || Session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.CambiarActivoAsync(
                AlmacenSeleccionado.Id, Session.Usuario.Id, ct);
            if (r.Success) { SuccessMessage = "Estado actualizado."; await LoadAsync(ct); }
            else           { ErrorMessage   = r.Error ?? "No se pudo cambiar el estado."; }
        }
        catch (TaskCanceledException) { }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(PuedeEliminar))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (AlmacenSeleccionado is null || Session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.EliminarAsync(
                AlmacenSeleccionado.Id, Session.Usuario.Id, ct);
            if (r.Success) { SuccessMessage = "Almacén eliminado."; await LoadAsync(ct); }
            else           { ErrorMessage   = r.Error ?? "No se pudo eliminar."; }
        }
        catch (TaskCanceledException) { }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Guarda (crea o actualiza) un almacén y recarga el grid.
    /// Invocado desde el code-behind de la Page tras confirmar el diálogo.
    /// </summary>
    public async Task<bool> GuardarAlmacenAsync(
        AlmacenDto? original, string nombre, string? codigo, string? descripcion,
        CancellationToken ct = default)
    {
        if (Session.Usuario is null) return false;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            if (original is null)
            {
                var dto = new CrearAlmacenDto(
                    Session.EmpresaId, Session.SucursalId, nombre, codigo, descripcion);
                var r = await _service.CrearAsync(dto, Session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo crear."; return false; }
                SuccessMessage = $"Almacén '{r.Value!.Nombre}' creado.";
            }
            else
            {
                var dto = new ActualizarAlmacenDto(nombre, codigo, descripcion);
                var r = await _service.ActualizarAsync(original.Id, dto, Session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo actualizar."; return false; }
                SuccessMessage = "Almacén actualizado.";
            }
            await LoadAsync(ct);
            return true;
        }
        catch (TaskCanceledException) { return false; }
        finally { IsBusy = false; }
    }

    private bool HaySeleccion()         => AlmacenSeleccionado is not null;
    private bool PuedeMarcarPrincipal() => AlmacenSeleccionado is not null
                                        && !AlmacenSeleccionado.EsPrincipal
                                        && AlmacenSeleccionado.Activo;
    private bool PuedeEliminar()        => AlmacenSeleccionado is not null
                                        && !AlmacenSeleccionado.EsPrincipal;
}

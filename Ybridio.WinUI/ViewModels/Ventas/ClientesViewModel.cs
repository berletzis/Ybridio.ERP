using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Application.Services.Venta;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.ViewModels.Ventas;

/// <summary>
/// ViewModel del sub-módulo Ventas > Clientes.
/// CRUD de clientes con enforcement de autorización (cliente.ver / cliente.crear / cliente.editar).
/// </summary>
public sealed partial class ClientesViewModel : BaseContextViewModel
{
    private readonly IClienteService                  _service;
    private readonly IErpAuthorizationService         _auth;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;

    [ObservableProperty] private bool       isBusy;
    [ObservableProperty] private string     errorMessage   = string.Empty;
    [ObservableProperty] private string     successMessage = string.Empty;
    [ObservableProperty] private string     busqueda       = string.Empty;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    [NotifyCanExecuteChangedFor(nameof(EliminarCommand))]
    private ClienteDto? clienteSeleccionado;

    private IReadOnlyList<ClienteDto> _todos = [];
    public ObservableCollection<ClienteDto> Clientes { get; } = [];

    public Visibility IsEmptyState => Clientes.Count == 0 && !IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public Action<ClienteDto?>? SolicitarNuevoEditar;

    public ClientesViewModel(
        IClienteService                  service,
        IErpAuthorizationService         auth,
        SessionService                   session,
        IOperationalObservabilityService observability,
        ICurrentContextTracker           contextTracker)
        : base(session)
    {
        _service        = service;
        _auth           = auth;
        _observability  = observability;
        _contextTracker = contextTracker;
        Clientes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyState));
    }

    private bool HaySeleccion => ClienteSeleccionado is not null;

    partial void OnBusquedaChanged(string value) => AplicarFiltro();
    partial void OnIsBusyChanged(bool value)     => OnPropertyChanged(nameof(IsEmptyState));

    [RelayCommand] private void Nuevo() => SolicitarNuevoEditar?.Invoke(null);
    [RelayCommand(CanExecute = nameof(HaySeleccion))] private void Editar() => SolicitarNuevoEditar?.Invoke(ClienteSeleccionado);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (ClienteSeleccionado is null || Session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.EliminarAsync(ClienteSeleccionado.Id, Session.Usuario.Id, ct);
            if (r.Success) { SuccessMessage = "Cliente eliminado."; await LoadAsync(ct); }
            else           { ErrorMessage   = r.Error ?? "No se pudo eliminar."; }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (Session.EmpresaId == 0) return;
        IsBusy = true; ErrorMessage = string.Empty;
        var sw = Stopwatch.StartNew();
        try
        {
            if (!await _auth.PuedeAsync(PermisosClave.Cliente.Ver, ct))
            {
                sw.Stop();
                ErrorMessage = "Sin permiso para ver clientes (cliente.ver).";
                ReportContext(sw.Elapsed, denied: true);
                return;
            }
            _todos = await _service.ListarPorEmpresaAsync(Session.EmpresaId, ct);
            AplicarFiltro();
            sw.Stop(); ReportContext(sw.Elapsed);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ErrorMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    public async Task<bool> GuardarAsync(ClienteDto? existente,
        string nombre, string? rfc, string? email, string? telefono,
        string? direccion, string? notas, decimal limiteCredito, CancellationToken ct = default)
    {
        if (Session.Usuario is null) return false;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            if (existente is null)
            {
                var dto = new CrearClienteDto(Session.EmpresaId, nombre, rfc, email, telefono, direccion, notas, limiteCredito);
                var r   = await _service.CrearAsync(dto, Session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo crear."; return false; }
                SuccessMessage = "Cliente creado.";
            }
            else
            {
                var dto = new ActualizarClienteDto(nombre, rfc, email, telefono, direccion, notas, limiteCredito);
                var r   = await _service.ActualizarAsync(existente.Id, dto, Session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo actualizar."; return false; }
                SuccessMessage = "Cliente actualizado.";
            }
            await LoadAsync(ct);
            return true;
        }
        finally { IsBusy = false; }
    }

    protected override Task OnContextChangedAsync() => LoadAsync();
    public void ReportLiveContext() => _contextTracker.SetViewModelContext(BuildCurrentContext());

    private void AplicarFiltro()
    {
        Clientes.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t) ? _todos
            : _todos.Where(c => c.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                 (c.RFC?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                 (c.Telefono?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false));
        foreach (var c in lista) Clientes.Add(c);
        OnPropertyChanged(nameof(IsEmptyState));
    }

    private void ReportContext(TimeSpan duration, bool denied = false)
    {
        _contextTracker.SetViewModelContext(BuildCurrentContext(denied));
        _observability.Report(new GridOperationContext(
            Module: "Ventas", SubModule: "Clientes", ViewModel: nameof(ClientesViewModel),
            Entity: "catalogos.Cliente", RecordCount: Clientes.Count, Duration: duration,
            EmpresaFilter:    new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing, Session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected, Note: "Clientes son globales por empresa"),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "No aplica"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            SearchTerm: string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos: false, CategoriaFiltro: null, FiltroTemporal: null,
            Notes: denied ? [$"ACCESO DENEGADO — permiso requerido: {PermisosClave.Cliente.Ver}"] : [$"Empresa={Session.EmpresaId}"],
            Timestamp: DateTime.Now));
    }

    private CurrentOperationalContext BuildCurrentContext(bool denied = false) =>
        new(Module: "Ventas", SubModule: "Clientes", ViewModel: nameof(ClientesViewModel),
            Entity: "catalogos.Cliente", RecordCount: Clientes.Count,
            SearchTerm: string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos: false, CategoriaFiltro: denied ? $"DENEGADO ({PermisosClave.Cliente.Ver})" : null,
            FiltroTemporal: null,
            EmpresaFilter:    new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing, Session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected, Note: "Clientes globales por empresa"),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "No aplica"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            Source: "ModuleFrame", UpdatedAt: DateTime.Now);
}

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
using Ybridio.Application.DTOs.Inventario;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Application.Services.Inventario;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.ViewModels.Inventario;

/// <summary>
/// ViewModel del sub-módulo Inventario > Existencias.
/// Consulta el stock actual con enforcement de autorización (existencia.ver) y scope de almacén.
/// Si el usuario tiene almacenes restringidos, solo se muestran las existencias de esos almacenes.
/// </summary>
public sealed partial class ExistenciasViewModel : BaseContextViewModel
{
    private readonly IInventarioService               _inventario;
    private readonly IErpAuthorizationService         _auth;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;

    [ObservableProperty] private bool               isBusy;
    [ObservableProperty] private string             errorMessage   = string.Empty;
    [ObservableProperty] private string             successMessage = string.Empty;
    [ObservableProperty] private string             busqueda       = string.Empty;
    [ObservableProperty] private ExistenciaDto?     existenciaSeleccionada;

    private IReadOnlyList<ExistenciaDto> _todas = [];

    public ObservableCollection<ExistenciaDto> Existencias { get; } = [];

    public Visibility IsEmptyState =>
        Existencias.Count == 0 && !IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public ExistenciasViewModel(
        IInventarioService               inventario,
        IErpAuthorizationService         auth,
        SessionService                   session,
        IOperationalObservabilityService observability,
        ICurrentContextTracker           contextTracker)
        : base(session)
    {
        _inventario     = inventario;
        _auth           = auth;
        _observability  = observability;
        _contextTracker = contextTracker;
        Existencias.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyState));
    }

    partial void OnBusquedaChanged(string value) => AplicarFiltro();
    partial void OnIsBusyChanged(bool value)     => OnPropertyChanged(nameof(IsEmptyState));

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (Session.EmpresaId == 0) return;

        IsBusy       = true;
        ErrorMessage = string.Empty;
        var sw = Stopwatch.StartNew();

        try
        {
            // ── Pre-check de autorización ──────────────────────────────────────
            if (!await _auth.PuedeAsync(PermisosClave.Existencia.Ver, ct))
            {
                sw.Stop();
                ErrorMessage = "Sin permiso para ver existencias (existencia.ver).";
                _observability.Report(BuildOperationalContext(sw.Elapsed, denied: true));
                _contextTracker.SetViewModelContext(BuildCurrentContext(denied: true));
                return;
            }

            // ── Consulta con scope de almacén aplicado ─────────────────────────
            var result = await _inventario.ListarExistenciasSeguraAsync(Session.EmpresaId, ct: ct);

            if (!result.Success)
            {
                sw.Stop();
                ErrorMessage = result.Error ?? "Error al cargar existencias.";
                _observability.Report(BuildOperationalContext(sw.Elapsed,
                    denied: result.ErrorCode == ErrorCode.Unauthorized));
                return;
            }

            _todas = result.Value!;
            AplicarFiltro();
            sw.Stop();
            _observability.Report(BuildOperationalContext(sw.Elapsed));
            _contextTracker.SetViewModelContext(BuildCurrentContext());
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ErrorMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    protected override Task OnContextChangedAsync() => LoadAsync();

    public void ReportLiveContext()
        => _contextTracker.SetViewModelContext(BuildCurrentContext());

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void AplicarFiltro()
    {
        Existencias.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t)
            ? _todas
            : _todas.Where(e =>
                e.ProductoNombre.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                e.ProductoCodigo.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                e.AlmacenNombre.Contains(t, StringComparison.OrdinalIgnoreCase));
        foreach (var e in lista.OrderBy(e => e.ProductoNombre)) Existencias.Add(e);
        OnPropertyChanged(nameof(IsEmptyState));
    }

    private GridOperationContext BuildOperationalContext(TimeSpan duration, bool denied = false) =>
        new(
            Module:           "Inventario",
            SubModule:        "Existencias",
            ViewModel:        nameof(ExistenciasViewModel),
            Entity:           "inventario.Existencia",
            RecordCount:      Existencias.Count,
            Duration:         duration,
            EmpresaFilter:    new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing,
                                  Session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected,
                                  Note: "Existencias filtra por almacén (scope), no por sucursal directamente"),
            AlmacenFilter:    new(FilterState.Applied,
                                  Note: "Scope de almacén aplicado via ISecurityScopeResolver"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false (global)"),
            SearchTerm:       string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos:      false,
            CategoriaFiltro:  null,
            FiltroTemporal:   null,
            Notes:            denied
                                ? [$"ACCESO DENEGADO — permiso requerido: {PermisosClave.Existencia.Ver}"]
                                : [$"Scope almacén: usuario restringido a sus almacenes asignados",
                                   $"Empresa={Session.EmpresaId}"],
            Timestamp:        DateTime.Now
        );

    private CurrentOperationalContext BuildCurrentContext(bool denied = false) =>
        new(
            Module:           "Inventario",
            SubModule:        "Existencias",
            ViewModel:        nameof(ExistenciasViewModel),
            Entity:           "inventario.Existencia",
            RecordCount:      Existencias.Count,
            SearchTerm:       string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos:      false,
            CategoriaFiltro:  denied ? $"DENEGADO ({PermisosClave.Existencia.Ver})" : null,
            FiltroTemporal:   null,
            EmpresaFilter:    new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing,
                                  Session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected,
                                  Note: "Existencias filtra por almacén"),
            AlmacenFilter:    new(FilterState.Applied, Note: "Scope via ISecurityScopeResolver"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            Source:           "ModuleFrame",
            UpdatedAt:        DateTime.Now
        );
}

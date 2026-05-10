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
/// ViewModel del sub-módulo Inventario > Entradas.
/// Gestiona la lista de entradas con enforcement de autorización (entrada.ver) y scope de sucursal.
/// </summary>
public sealed partial class EntradasViewModel : BaseContextViewModel
{
    private readonly IEntradaService                  _entradas;
    private readonly IErpAuthorizationService         _auth;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;

    [ObservableProperty] private bool              isBusy;
    [ObservableProperty] private string            mensajeEstado  = string.Empty;
    [ObservableProperty] private string            errorMessage   = string.Empty;
    [ObservableProperty] private string            successMessage = string.Empty;
    [ObservableProperty] private string            busqueda       = string.Empty;

    [ObservableProperty]
    private string filtroTemporal = "30 días";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    [NotifyCanExecuteChangedFor(nameof(EliminarCommand))]
    private EntradaResumenDto? entradaSeleccionada;

    private bool _isRefreshing;

    private IReadOnlyList<EntradaResumenDto> _todas = [];

    public ObservableCollection<EntradaResumenDto> Entradas { get; } = [];

    public Visibility IsEmptyState =>
        Entradas.Count == 0 && !IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public EntradasViewModel(
        IEntradaService                  entradas,
        IErpAuthorizationService         auth,
        SessionService                   session,
        IOperationalObservabilityService observability,
        ICurrentContextTracker           contextTracker)
        : base(session)
    {
        _entradas       = entradas;
        _auth           = auth;
        _observability  = observability;
        _contextTracker = contextTracker;
        Entradas.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyState));
    }

    private bool HaySeleccion => EntradaSeleccionada is not null;

    partial void OnBusquedaChanged(string value)       => AplicarFiltro();
    partial void OnFiltroTemporalChanged(string value) => _ = RefrescarAsync();
    partial void OnIsBusyChanged(bool value)           => OnPropertyChanged(nameof(IsEmptyState));

    [RelayCommand] private void Nuevo() { }
    [RelayCommand(CanExecute = nameof(HaySeleccion))] private void Editar() { }
    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default) => await Task.CompletedTask;
    [RelayCommand]
    private async Task ImportarAsync(CancellationToken ct = default) => await Task.CompletedTask;
    [RelayCommand]
    private async Task ExportarAsync(CancellationToken ct = default) => await Task.CompletedTask;

    [RelayCommand]
    public async Task RefrescarAsync(CancellationToken ct = default)
    {
        if (_isRefreshing) return;
        if (Session.EmpresaId == 0 || Session.SucursalId == 0) return;

        _isRefreshing = true;

        IsBusy       = true;
        ErrorMessage = string.Empty;
        var sw = Stopwatch.StartNew();

        try
        {
            // ── Pre-check de autorización ──────────────────────────────────────
            if (!await _auth.PuedeAsync(PermisosClave.Entrada.Ver, ct))
            {
                sw.Stop();
                ErrorMessage = "Sin permiso para ver entradas (entrada.ver).";
                _observability.Report(BuildOperationalContext(sw.Elapsed, denied: true, permiso: PermisosClave.Entrada.Ver));
                _contextTracker.SetViewModelContext(BuildCurrentContext(denied: true, permiso: PermisosClave.Entrada.Ver));
                return;
            }

            var (desde, hasta) = ParseFiltroTemporal();
            var result = await _entradas.ListarAsync(Session.EmpresaId, Session.SucursalId, desde, hasta, ct);

            if (!result.Success)
            {
                sw.Stop();
                ErrorMessage = result.Error ?? "Error al cargar entradas.";
                _observability.Report(BuildOperationalContext(sw.Elapsed,
                    denied: result.ErrorCode == ErrorCode.Unauthorized,
                    permiso: result.ErrorCode == ErrorCode.Unauthorized ? PermisosClave.Entrada.Ver : null));
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
        finally
        {
            IsBusy = false;
            _isRefreshing = false;
        }
    }

    protected override Task OnContextChangedAsync() => RefrescarAsync();

    public void ReportLiveContext()
        => _contextTracker.SetViewModelContext(BuildCurrentContext());

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void AplicarFiltro()
    {
        Entradas.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t)
            ? _todas
            : _todas.Where(e =>
                (e.Folio?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false) ||
                e.AlmacenNombre.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                e.ConceptoNombre.Contains(t, StringComparison.OrdinalIgnoreCase));
        foreach (var e in lista) Entradas.Add(e);
        OnPropertyChanged(nameof(IsEmptyState));
    }

    private (DateTime? desde, DateTime? hasta) ParseFiltroTemporal()
    {
        var ahora = DateTime.Now;
        return FiltroTemporal switch
        {
            "Hoy"      => (ahora.Date,                      ahora.Date.AddDays(1).AddTicks(-1)),
            "7 días"   => (ahora.AddDays(-7),               null),
            "30 días"  => (ahora.AddDays(-30),              null),
            "90 días"  => (ahora.AddDays(-90),              null),
            "6 meses"  => (ahora.AddMonths(-6),             null),
            "1 año"    => (ahora.AddYears(-1),              null),
            _          => (null,                            null)   // "Todo"
        };
    }

    private GridOperationContext BuildOperationalContext(
        TimeSpan duration, bool denied = false, string? permiso = null) =>
        new(
            Module:           "Inventario",
            SubModule:        "Entradas",
            ViewModel:        nameof(EntradasViewModel),
            Entity:           "inventario.Entrada",
            RecordCount:      Entradas.Count,
            Duration:         duration,
            EmpresaFilter:    new(Session.EmpresaId  != 0 ? FilterState.Applied : FilterState.Missing,
                                  Session.EmpresaId.ToString()),
            SucursalFilter:   new(Session.SucursalId != 0 ? FilterState.Applied : FilterState.Missing,
                                  Session.SucursalId.ToString()),
            AlmacenFilter:    new(FilterState.OmittedExpected,
                                  Note: "AlmacenId en Entrada es campo de documento, no filtro de consulta"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false (global)"),
            SearchTerm:       string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos:      false,
            CategoriaFiltro:  null,
            FiltroTemporal:   FiltroTemporal,
            Notes:            denied
                                ? [$"ACCESO DENEGADO — permiso requerido: {permiso}"]
                                : [$"Scope: Empresa={Session.EmpresaId} Sucursal={Session.SucursalId}"],
            Timestamp:        DateTime.Now
        );

    private CurrentOperationalContext BuildCurrentContext(bool denied = false, string? permiso = null) =>
        new(
            Module:           "Inventario",
            SubModule:        "Entradas",
            ViewModel:        nameof(EntradasViewModel),
            Entity:           "inventario.Entrada",
            RecordCount:      Entradas.Count,
            SearchTerm:       string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos:      false,
            CategoriaFiltro:  denied ? $"DENEGADO ({permiso})" : null,
            FiltroTemporal:   FiltroTemporal,
            EmpresaFilter:    new(Session.EmpresaId  != 0 ? FilterState.Applied : FilterState.Missing,
                                  Session.EmpresaId.ToString()),
            SucursalFilter:   new(Session.SucursalId != 0 ? FilterState.Applied : FilterState.Missing,
                                  Session.SucursalId.ToString()),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "Campo de documento — no es filtro de lista"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            Source:           "ModuleFrame",
            UpdatedAt:        DateTime.Now
        );
}

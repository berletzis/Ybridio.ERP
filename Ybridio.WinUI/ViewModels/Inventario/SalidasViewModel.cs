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
/// ViewModel del sub-módulo Inventario > Salidas.
/// Gestiona la lista de salidas con enforcement de autorización (salida.ver) y scope de sucursal.
/// La operación de autorización de salida requiere permiso <c>salida.autorizar</c>.
/// </summary>
public sealed partial class SalidasViewModel : BaseContextViewModel
{
    private readonly ISalidaService                   _salidas;
    private readonly IErpAuthorizationService         _auth;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;

    [ObservableProperty] private bool             isBusy;
    [ObservableProperty] private string           mensajeEstado  = string.Empty;
    [ObservableProperty] private string           errorMessage   = string.Empty;
    [ObservableProperty] private string           successMessage = string.Empty;
    [ObservableProperty] private string           busqueda       = string.Empty;

    [ObservableProperty]
    private string filtroTemporal = "30 días";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    [NotifyCanExecuteChangedFor(nameof(EliminarCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutorizarCommand))]
    private SalidaResumenDto? salidaSeleccionada;

    private IReadOnlyList<SalidaResumenDto> _todas = [];

    public ObservableCollection<SalidaResumenDto> Salidas { get; } = [];

    public Visibility IsEmptyState =>
        Salidas.Count == 0 && !IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public SalidasViewModel(
        ISalidaService                   salidas,
        IErpAuthorizationService         auth,
        SessionService                   session,
        IOperationalObservabilityService observability,
        ICurrentContextTracker           contextTracker)
        : base(session)
    {
        _salidas        = salidas;
        _auth           = auth;
        _observability  = observability;
        _contextTracker = contextTracker;
        Salidas.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyState));
    }

    private bool HaySeleccion      => SalidaSeleccionada is not null;
    private bool PuedoAutorizar    => SalidaSeleccionada is { TieneAutorizacion: false };

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

    [RelayCommand(CanExecute = nameof(PuedoAutorizar))]
    private async Task AutorizarAsync(CancellationToken ct = default)
    {
        if (SalidaSeleccionada is null || Session.Usuario is null) return;

        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var result = await _salidas.AutorizarAsync(SalidaSeleccionada.Id, Session.Usuario.Id, ct);
            if (result.Success)
            {
                SuccessMessage = "Salida autorizada.";
                await RefrescarAsync(ct);
            }
            else
            {
                ErrorMessage = result.Error ?? "No se pudo autorizar la salida.";
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task RefrescarAsync(CancellationToken ct = default)
    {
        if (Session.EmpresaId == 0 || Session.SucursalId == 0) return;

        IsBusy       = true;
        ErrorMessage = string.Empty;
        var sw = Stopwatch.StartNew();

        try
        {
            // ── Pre-check de autorización ──────────────────────────────────────
            if (!await _auth.PuedeAsync(PermisosClave.Salida.Ver, ct))
            {
                sw.Stop();
                ErrorMessage = "Sin permiso para ver salidas (salida.ver).";
                _observability.Report(BuildOperationalContext(sw.Elapsed, denied: true, permiso: PermisosClave.Salida.Ver));
                _contextTracker.SetViewModelContext(BuildCurrentContext(denied: true, permiso: PermisosClave.Salida.Ver));
                return;
            }

            var (desde, hasta) = ParseFiltroTemporal();
            var result = await _salidas.ListarAsync(Session.EmpresaId, Session.SucursalId, desde, hasta, ct);

            if (!result.Success)
            {
                sw.Stop();
                ErrorMessage = result.Error ?? "Error al cargar salidas.";
                _observability.Report(BuildOperationalContext(sw.Elapsed,
                    denied: result.ErrorCode == ErrorCode.Unauthorized,
                    permiso: result.ErrorCode == ErrorCode.Unauthorized ? PermisosClave.Salida.Ver : null));
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

    protected override Task OnContextChangedAsync() => RefrescarAsync();

    public void ReportLiveContext()
        => _contextTracker.SetViewModelContext(BuildCurrentContext());

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void AplicarFiltro()
    {
        Salidas.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t)
            ? _todas
            : _todas.Where(s =>
                (s.Folio?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false) ||
                s.AlmacenNombre.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                s.ConceptoNombre.Contains(t, StringComparison.OrdinalIgnoreCase));
        foreach (var s in lista) Salidas.Add(s);
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
            _          => (null,                            null)
        };
    }

    private GridOperationContext BuildOperationalContext(
        TimeSpan duration, bool denied = false, string? permiso = null) =>
        new(
            Module:           "Inventario",
            SubModule:        "Salidas",
            ViewModel:        nameof(SalidasViewModel),
            Entity:           "inventario.Salida",
            RecordCount:      Salidas.Count,
            Duration:         duration,
            EmpresaFilter:    new(Session.EmpresaId  != 0 ? FilterState.Applied : FilterState.Missing,
                                  Session.EmpresaId.ToString()),
            SucursalFilter:   new(Session.SucursalId != 0 ? FilterState.Applied : FilterState.Missing,
                                  Session.SucursalId.ToString()),
            AlmacenFilter:    new(FilterState.OmittedExpected,
                                  Note: "AlmacenId en Salida es campo de documento, no filtro de consulta"),
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
            SubModule:        "Salidas",
            ViewModel:        nameof(SalidasViewModel),
            Entity:           "inventario.Salida",
            RecordCount:      Salidas.Count,
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

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
using Ybridio.Application.DTOs.Finanzas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Application.Services.Finanzas;
using Ybridio.Domain.Finanzas;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.ViewModels.Finanzas;

/// <summary>
/// ViewModel del sub-módulo Finanzas > Gastos.
/// Lista gastos operativos con enforcement de autorización (finanzas.ver) y observabilidad integrada.
/// </summary>
public sealed partial class GastosViewModel : BaseContextViewModel
{
    private readonly IFinanzasService                 _service;
    private readonly IErpAuthorizationService         _auth;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;

    [ObservableProperty] private bool                    isBusy;
    [ObservableProperty] private string                  errorMessage   = string.Empty;
    [ObservableProperty] private string                  successMessage = string.Empty;
    [ObservableProperty] private string                  busqueda       = string.Empty;
    [ObservableProperty] private string                  filtroTemporal = "30 días";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    [NotifyCanExecuteChangedFor(nameof(EliminarCommand))]
    private MovimientoFinancieroDto? gastoSeleccionado;

    private IReadOnlyList<MovimientoFinancieroDto> _todos = [];
    public ObservableCollection<MovimientoFinancieroDto> Gastos { get; } = [];
    public IReadOnlyList<CategoriaFinancieraDto> Categorias { get; private set; } = [];

    public Visibility IsEmptyState => Gastos.Count == 0 && !IsBusy ? Visibility.Visible : Visibility.Collapsed;

    // Callbacks para diálogos (requieren XamlRoot desde la Page)
    public Action<MovimientoFinancieroDto?>? SolicitarNuevoEditar;

    public GastosViewModel(
        IFinanzasService                 service,
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
        Gastos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyState));
    }

    private bool HaySeleccion => GastoSeleccionado is not null;

    partial void OnBusquedaChanged(string value)       => AplicarFiltro();
    partial void OnFiltroTemporalChanged(string value) => _ = RefrescarAsync();
    partial void OnIsBusyChanged(bool value)           => OnPropertyChanged(nameof(IsEmptyState));

    [RelayCommand] private void Nuevo() => SolicitarNuevoEditar?.Invoke(null);
    [RelayCommand(CanExecute = nameof(HaySeleccion))] private void Editar() => SolicitarNuevoEditar?.Invoke(GastoSeleccionado);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (GastoSeleccionado is null || Session.Usuario is null) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var result = await _service.EliminarAsync(GastoSeleccionado.Id, Session.Usuario.Id, ct);
            if (result.Success) { SuccessMessage = "Gasto eliminado."; await RefrescarAsync(ct); }
            else                { ErrorMessage   = result.Error ?? "No se pudo eliminar."; }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task RefrescarAsync(CancellationToken ct = default)
    {
        if (Session.EmpresaId == 0) return;
        IsBusy       = true;
        ErrorMessage = string.Empty;
        var sw = Stopwatch.StartNew();
        try
        {
            if (!await _auth.PuedeAsync(PermisosClave.Finanzas.Ver, ct))
            {
                sw.Stop();
                ErrorMessage = "Sin permiso para ver finanzas (finanzas.ver).";
                ReportContext(sw.Elapsed, denied: true);
                return;
            }

            var (desde, hasta) = ParseFiltroTemporal();
            var result = await _service.ListarAsync(Session.EmpresaId, TipoMovimientoFinanciero.Gasto,
                null, desde, hasta, ct);

            if (!result.Success) { sw.Stop(); ErrorMessage = result.Error ?? "Error al cargar gastos."; ReportContext(sw.Elapsed, denied: result.ErrorCode == ErrorCode.Unauthorized); return; }

            _todos = result.Value!;
            Categorias = await _service.ListarCategoriasAsync(Session.EmpresaId, "Gasto", ct);
            AplicarFiltro();
            sw.Stop();
            ReportContext(sw.Elapsed);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ErrorMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    /// <summary>Guarda un gasto nuevo o actualizado desde el diálogo.</summary>
    public async Task<bool> GuardarAsync(MovimientoFinancieroDto? existente,
        string concepto, decimal monto, DateTime fecha, int? categoriaId, string? observaciones,
        CancellationToken ct = default)
    {
        if (Session.Usuario is null) return false;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            if (existente is null)
            {
                var dto = new CrearMovimientoFinancieroDto(Session.EmpresaId, Session.SucursalId != 0 ? Session.SucursalId : null,
                    TipoMovimientoFinanciero.Gasto, ContextoFinanciero.Empresa,
                    categoriaId, concepto, monto, fecha, observaciones);
                var r = await _service.CrearAsync(dto, Session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo crear."; return false; }
                SuccessMessage = "Gasto registrado.";
            }
            else
            {
                var dto = new ActualizarMovimientoFinancieroDto(categoriaId, concepto, monto, fecha, observaciones);
                var r = await _service.ActualizarAsync(existente.Id, dto, Session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo actualizar."; return false; }
                SuccessMessage = "Gasto actualizado.";
            }
            await RefrescarAsync(ct);
            return true;
        }
        finally { IsBusy = false; }
    }

    protected override Task OnContextChangedAsync() => RefrescarAsync();

    public void ReportLiveContext() => _contextTracker.SetViewModelContext(BuildCurrentContext());

    private void AplicarFiltro()
    {
        Gastos.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t) ? _todos
            : _todos.Where(g => g.Concepto.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                 (g.CategoriaNombre?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false));
        foreach (var g in lista) Gastos.Add(g);
        OnPropertyChanged(nameof(IsEmptyState));
    }

    private (DateTime? desde, DateTime? hasta) ParseFiltroTemporal()
    {
        var ahora = DateTime.Now;
        return FiltroTemporal switch
        {
            "Hoy"      => (ahora.Date,          ahora.Date.AddDays(1).AddTicks(-1)),
            "7 días"   => (ahora.AddDays(-7),   null),
            "30 días"  => (ahora.AddDays(-30),  null),
            "90 días"  => (ahora.AddDays(-90),  null),
            "6 meses"  => (ahora.AddMonths(-6), null),
            "1 año"    => (ahora.AddYears(-1),  null),
            _          => (null,                null)
        };
    }

    private void ReportContext(TimeSpan duration, bool denied = false)
    {
        var ctx = BuildCurrentContext(denied);
        _contextTracker.SetViewModelContext(ctx);
        _observability.Report(new GridOperationContext(
            Module: "Finanzas", SubModule: "Gastos", ViewModel: nameof(GastosViewModel),
            Entity: "finanzas.MovimientoFinanciero",
            RecordCount: Gastos.Count, Duration: duration,
            EmpresaFilter:    new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing, Session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected, Note: "Gastos filtran por empresa; sucursal opcional"),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "Finanzas no tiene dimensión de almacén"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false (global)"),
            SearchTerm: string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos: false, CategoriaFiltro: null, FiltroTemporal: FiltroTemporal,
            Notes: denied ? [$"ACCESO DENEGADO — permiso requerido: {PermisosClave.Finanzas.Ver}"]
                          : [$"Tipo: Gasto | Contexto: Empresa | Empresa={Session.EmpresaId}"],
            Timestamp: DateTime.Now));
    }

    private CurrentOperationalContext BuildCurrentContext(bool denied = false) =>
        new(Module: "Finanzas", SubModule: "Gastos", ViewModel: nameof(GastosViewModel),
            Entity: "finanzas.MovimientoFinanciero",
            RecordCount: Gastos.Count,
            SearchTerm: string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos: false,
            CategoriaFiltro: denied ? $"DENEGADO ({PermisosClave.Finanzas.Ver})" : null,
            FiltroTemporal: FiltroTemporal,
            EmpresaFilter:    new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing, Session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected, Note: "Finanzas Empresa"),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "No aplica"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            Source: "ModuleFrame", UpdatedAt: DateTime.Now);
}

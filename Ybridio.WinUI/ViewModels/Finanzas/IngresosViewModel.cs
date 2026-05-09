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
/// ViewModel del sub-módulo Finanzas > Ingresos.
/// Lista ingresos no provenientes de ventas con enforcement de autorización (finanzas.ver).
/// </summary>
public sealed partial class IngresosViewModel : BaseContextViewModel
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
    private MovimientoFinancieroDto? ingresoSeleccionado;

    private IReadOnlyList<MovimientoFinancieroDto> _todos = [];
    public ObservableCollection<MovimientoFinancieroDto> Ingresos { get; } = [];
    public IReadOnlyList<CategoriaFinancieraDto> Categorias { get; private set; } = [];

    public Visibility IsEmptyState => Ingresos.Count == 0 && !IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public Action<MovimientoFinancieroDto?>? SolicitarNuevoEditar;

    public IngresosViewModel(
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
        Ingresos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyState));
    }

    private bool HaySeleccion => IngresoSeleccionado is not null;

    partial void OnBusquedaChanged(string value)       => AplicarFiltro();
    partial void OnFiltroTemporalChanged(string value) => _ = RefrescarAsync();
    partial void OnIsBusyChanged(bool value)           => OnPropertyChanged(nameof(IsEmptyState));

    [RelayCommand] private void Nuevo() => SolicitarNuevoEditar?.Invoke(null);
    [RelayCommand(CanExecute = nameof(HaySeleccion))] private void Editar() => SolicitarNuevoEditar?.Invoke(IngresoSeleccionado);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (IngresoSeleccionado is null || Session.Usuario is null) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var result = await _service.EliminarAsync(IngresoSeleccionado.Id, Session.Usuario.Id, ct);
            if (result.Success) { SuccessMessage = "Ingreso eliminado."; await RefrescarAsync(ct); }
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
            var result = await _service.ListarAsync(Session.EmpresaId, TipoMovimientoFinanciero.Ingreso,
                null, desde, hasta, ct);

            if (!result.Success) { sw.Stop(); ErrorMessage = result.Error ?? "Error al cargar ingresos."; ReportContext(sw.Elapsed, denied: result.ErrorCode == ErrorCode.Unauthorized); return; }

            _todos = result.Value!;
            Categorias = await _service.ListarCategoriasAsync(Session.EmpresaId, "Ingreso", ct);
            AplicarFiltro();
            sw.Stop();
            ReportContext(sw.Elapsed);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ErrorMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

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
                var dto = new CrearMovimientoFinancieroDto(Session.EmpresaId, null,
                    TipoMovimientoFinanciero.Ingreso, ContextoFinanciero.Empresa,
                    categoriaId, concepto, monto, fecha, observaciones);
                var r = await _service.CrearAsync(dto, Session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo crear."; return false; }
                SuccessMessage = "Ingreso registrado.";
            }
            else
            {
                var dto = new ActualizarMovimientoFinancieroDto(categoriaId, concepto, monto, fecha, observaciones);
                var r = await _service.ActualizarAsync(existente.Id, dto, Session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo actualizar."; return false; }
                SuccessMessage = "Ingreso actualizado.";
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
        Ingresos.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t) ? _todos
            : _todos.Where(i => i.Concepto.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                 (i.CategoriaNombre?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false));
        foreach (var i in lista) Ingresos.Add(i);
        OnPropertyChanged(nameof(IsEmptyState));
    }

    private (DateTime? desde, DateTime? hasta) ParseFiltroTemporal()
    {
        var ahora = DateTime.Now;
        return FiltroTemporal switch
        {
            "Hoy"     => (ahora.Date, ahora.Date.AddDays(1).AddTicks(-1)),
            "7 días"  => (ahora.AddDays(-7), null),
            "30 días" => (ahora.AddDays(-30), null),
            "90 días" => (ahora.AddDays(-90), null),
            "6 meses" => (ahora.AddMonths(-6), null),
            "1 año"   => (ahora.AddYears(-1), null),
            _         => (null, null)
        };
    }

    private void ReportContext(TimeSpan duration, bool denied = false)
    {
        _contextTracker.SetViewModelContext(BuildCurrentContext(denied));
        _observability.Report(new GridOperationContext(
            Module: "Finanzas", SubModule: "Ingresos", ViewModel: nameof(IngresosViewModel),
            Entity: "finanzas.MovimientoFinanciero",
            RecordCount: Ingresos.Count, Duration: duration,
            EmpresaFilter:    new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing, Session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected, Note: "Ingresos filtran por empresa"),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "No aplica"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false (global)"),
            SearchTerm: string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos: false, CategoriaFiltro: null, FiltroTemporal: FiltroTemporal,
            Notes: denied ? [$"ACCESO DENEGADO — permiso requerido: {PermisosClave.Finanzas.Ver}"]
                          : [$"Tipo: Ingreso | Empresa={Session.EmpresaId}"],
            Timestamp: DateTime.Now));
    }

    private CurrentOperationalContext BuildCurrentContext(bool denied = false) =>
        new(Module: "Finanzas", SubModule: "Ingresos", ViewModel: nameof(IngresosViewModel),
            Entity: "finanzas.MovimientoFinanciero",
            RecordCount: Ingresos.Count,
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

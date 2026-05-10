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
using Ybridio.Domain.Ventas;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.ViewModels.Ventas;

/// <summary>ViewModel del sub-módulo Ventas > Órdenes de Trabajo.</summary>
public sealed partial class OrdenesTrabajoViewModel : BaseContextViewModel
{
    private readonly IOrdenTrabajoService             _service;
    private readonly IErpAuthorizationService         _auth;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;

    [ObservableProperty] private bool          isBusy;
    [ObservableProperty] private string        errorMessage   = string.Empty;
    [ObservableProperty] private string        successMessage = string.Empty;
    [ObservableProperty] private string        busqueda       = string.Empty;
    [ObservableProperty] private string        filtroTemporal = "30 días";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EliminarCommand))]
    [NotifyCanExecuteChangedFor(nameof(AvanzarEstatusCommand))]
    [NotifyCanExecuteChangedFor(nameof(AgregarMaterialCommand))]
    private OTResumenDto? otSeleccionada;

    private bool _isRefreshing;

    private IReadOnlyList<OTResumenDto> _todas = [];
    public ObservableCollection<OTResumenDto> OrdenesTrabajo { get; } = [];
    public Visibility IsEmptyState => OrdenesTrabajo.Count == 0 && !IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public Action<OTResumenDto>? SolicitarAgregarMaterial;

    public OrdenesTrabajoViewModel(
        IOrdenTrabajoService             service,
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
        OrdenesTrabajo.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyState));
    }

    private bool HaySeleccion  => OtSeleccionada is not null;
    private bool EsActiva      => OtSeleccionada is { Estatus: not EstatusOrdenTrabajo.Entregada and not EstatusOrdenTrabajo.Cancelada };

    partial void OnBusquedaChanged(string value)       => AplicarFiltro();
    partial void OnFiltroTemporalChanged(string value) => _ = RefrescarAsync();
    partial void OnIsBusyChanged(bool value)           => OnPropertyChanged(nameof(IsEmptyState));

    [RelayCommand(CanExecute = nameof(EsActiva))] private void AgregarMaterial() => SolicitarAgregarMaterial?.Invoke(OtSeleccionada!);

    [RelayCommand(CanExecute = nameof(EsActiva))]
    private async Task AvanzarEstatusAsync(CancellationToken ct = default)
    {
        if (OtSeleccionada is null || Session.Usuario is null) return;
        var siguiente = OtSeleccionada.Estatus switch
        {
            EstatusOrdenTrabajo.Nueva             => EstatusOrdenTrabajo.EnProceso,
            EstatusOrdenTrabajo.EnProceso         => EstatusOrdenTrabajo.Terminada,
            EstatusOrdenTrabajo.EsperandoMaterial => EstatusOrdenTrabajo.EnProceso,
            EstatusOrdenTrabajo.Terminada         => EstatusOrdenTrabajo.Entregada,
            _                                     => OtSeleccionada.Estatus
        };
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.CambiarEstatusAsync(OtSeleccionada.Id, siguiente, Session.Usuario.Id, ct);
            if (r.Success) { SuccessMessage = $"OT → {siguiente}."; await RefrescarAsync(ct); }
            else           { ErrorMessage   = r.Error ?? "No se pudo avanzar."; }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (OtSeleccionada is null || Session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.EliminarAsync(OtSeleccionada.Id, Session.Usuario.Id, ct);
            if (r.Success) { SuccessMessage = "OT eliminada."; await RefrescarAsync(ct); }
            else           { ErrorMessage   = r.Error ?? "No se pudo eliminar."; }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task RefrescarAsync(CancellationToken ct = default)
    {
        if (_isRefreshing) return;
        if (Session.EmpresaId == 0) return;

        _isRefreshing = true;
        IsBusy = true; ErrorMessage = string.Empty;
        var sw = Stopwatch.StartNew();
        try
        {
            if (!await _auth.PuedeAsync(PermisosClave.OrdenTrabajo.Ver, ct))
            {
                sw.Stop(); ErrorMessage = "Sin permiso (ordentrabajo.ver).";
                ReportContext(sw.Elapsed, denied: true); return;
            }
            var (desde, hasta) = ParseFiltroTemporal();
            var result = await _service.ListarAsync(Session.EmpresaId, null, desde, hasta, ct);
            if (!result.Success) { sw.Stop(); ErrorMessage = result.Error ?? "Error."; ReportContext(sw.Elapsed, denied: result.ErrorCode == ErrorCode.Unauthorized); return; }
            _todas = result.Value!;
            AplicarFiltro(); sw.Stop(); ReportContext(sw.Elapsed);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ErrorMessage = $"Error: {ex.Message}"; }
        finally
        {
            IsBusy = false;
            _isRefreshing = false;
        }
    }

    public Task<ServiceResult<DetalleLineaDto>> AgregarMaterialAsync(long otId, AgregarOTMaterialDto dto, CancellationToken ct = default)
        => Session.Usuario is null
            ? Task.FromResult(ServiceResult<DetalleLineaDto>.Fail("Sin sesión."))
            : _service.AgregarMaterialAsync(otId, dto, Session.Usuario.Id, ct);

    protected override Task OnContextChangedAsync() => RefrescarAsync();
    public void ReportLiveContext() => _contextTracker.SetViewModelContext(BuildCurrentContext());

    private void AplicarFiltro()
    {
        OrdenesTrabajo.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t) ? _todas
            : _todas.Where(o => o.NombreCliente.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                 o.Descripcion.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                 o.EstatusTexto.Contains(t, StringComparison.OrdinalIgnoreCase));
        foreach (var o in lista) OrdenesTrabajo.Add(o);
        OnPropertyChanged(nameof(IsEmptyState));
    }

    private (DateTime? desde, DateTime? hasta) ParseFiltroTemporal()
    {
        var ahora = DateTime.Now;
        return FiltroTemporal switch
        {
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
            Module: "Ventas", SubModule: "OrdenesTrabajo", ViewModel: nameof(OrdenesTrabajoViewModel),
            Entity: "ventas.OrdenTrabajo", RecordCount: OrdenesTrabajo.Count, Duration: duration,
            EmpresaFilter:    new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing, Session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected, Note: "OT por empresa"),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "No aplica"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            SearchTerm: string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos: false, CategoriaFiltro: null, FiltroTemporal: FiltroTemporal,
            Notes: denied ? [$"ACCESO DENEGADO — {PermisosClave.OrdenTrabajo.Ver}"] : [$"Empresa={Session.EmpresaId}"],
            Timestamp: DateTime.Now));
    }

    private CurrentOperationalContext BuildCurrentContext(bool denied = false) =>
        new(Module: "Ventas", SubModule: "OrdenesTrabajo", ViewModel: nameof(OrdenesTrabajoViewModel),
            Entity: "ventas.OrdenTrabajo", RecordCount: OrdenesTrabajo.Count,
            SearchTerm: string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos: false, CategoriaFiltro: denied ? $"DENEGADO ({PermisosClave.OrdenTrabajo.Ver})" : null,
            FiltroTemporal: FiltroTemporal,
            EmpresaFilter:    new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing, Session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected, Note: "OT por empresa"),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "No aplica"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            Source: "ModuleFrame", UpdatedAt: DateTime.Now);
}

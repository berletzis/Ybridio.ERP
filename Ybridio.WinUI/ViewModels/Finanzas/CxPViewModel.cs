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
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.ViewModels.Finanzas;

/// <summary>
/// ViewModel del sub-módulo Finanzas > Cuentas por Pagar.
/// Lista CxP con saldo pendiente y vencimiento. Permite registro de pagos.
/// </summary>
public sealed partial class CxPViewModel : BaseContextViewModel
{
    private readonly ICxPService                      _service;
    private readonly IErpAuthorizationService         _auth;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;

    [ObservableProperty] private bool    isBusy;
    [ObservableProperty] private string  errorMessage   = string.Empty;
    [ObservableProperty] private string  successMessage = string.Empty;
    [ObservableProperty] private string  busqueda       = string.Empty;
    [ObservableProperty] private string  filtroTemporal = "30 días";
    [ObservableProperty] private bool    soloVigentes   = true;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EliminarCommand))]
    [NotifyCanExecuteChangedFor(nameof(RegistrarPagoCommand))]
    private CxPDto? cxpSeleccionada;

    private bool _isRefreshing;

    private IReadOnlyList<CxPDto> _todas = [];
    public ObservableCollection<CxPDto> CxPItems { get; } = [];

    public Visibility IsEmptyState => CxPItems.Count == 0 && !IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public Action<CxPDto?>? SolicitarNuevo;
    public Action<CxPDto>?  SolicitarRegistrarPago;

    public CxPViewModel(
        ICxPService                      service,
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
        CxPItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyState));
    }

    private bool HaySeleccion => CxpSeleccionada is not null;
    private bool TieneSaldo   => CxpSeleccionada is { SaldoPendiente: > 0 };

    partial void OnBusquedaChanged(string value)       => AplicarFiltro();
    partial void OnFiltroTemporalChanged(string value) => _ = RefrescarAsync();
    partial void OnSoloVigentesChanged(bool value)     => _ = RefrescarAsync();
    partial void OnIsBusyChanged(bool value)           => OnPropertyChanged(nameof(IsEmptyState));

    [RelayCommand] private void Nuevo() => SolicitarNuevo?.Invoke(null);
    [RelayCommand(CanExecute = nameof(TieneSaldo))] private void RegistrarPago() => SolicitarRegistrarPago?.Invoke(CxpSeleccionada!);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (CxpSeleccionada is null || Session.Usuario is null) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.EliminarAsync(CxpSeleccionada.Id, Session.Usuario.Id, ct);
            if (r.Success) { SuccessMessage = "CxP eliminada."; await RefrescarAsync(ct); }
            else           { ErrorMessage   = r.Error ?? "No se pudo eliminar."; }
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
            if (!await _auth.PuedeAsync(PermisosClave.CxP.Ver, ct))
            {
                sw.Stop();
                ErrorMessage = "Sin permiso para ver cuentas por pagar (cxp.ver).";
                ReportContext(sw.Elapsed, denied: true);
                return;
            }

            var (desde, hasta) = ParseFiltroTemporal();
            var result = await _service.ListarAsync(Session.EmpresaId, null, SoloVigentes, desde, hasta, ct);

            if (!result.Success) { sw.Stop(); ErrorMessage = result.Error ?? "Error al cargar CxP."; ReportContext(sw.Elapsed, denied: result.ErrorCode == ErrorCode.Unauthorized); return; }

            _todas = result.Value!;
            AplicarFiltro();
            sw.Stop();
            ReportContext(sw.Elapsed);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ErrorMessage = $"Error: {ex.Message}"; }
        finally
        {
            IsBusy = false;
            _isRefreshing = false;
        }
    }

    public async Task<bool> CrearCxPAsync(CrearCxPDto dto, CancellationToken ct = default)
    {
        if (Session.Usuario is null) return false;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.CrearAsync(dto with { EmpresaId = Session.EmpresaId }, Session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo crear."; return false; }
            SuccessMessage = "Cuenta por pagar registrada.";
            await RefrescarAsync(ct);
            return true;
        }
        finally { IsBusy = false; }
    }

    public async Task<bool> PagarAsync(long cxpId, decimal monto, CancellationToken ct = default)
    {
        if (Session.Usuario is null) return false;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.RegistrarPagoAsync(new(cxpId, monto), Session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo registrar el pago."; return false; }
            SuccessMessage = "Pago registrado.";
            await RefrescarAsync(ct);
            return true;
        }
        finally { IsBusy = false; }
    }

    protected override Task OnContextChangedAsync() => RefrescarAsync();
    public void ReportLiveContext() => _contextTracker.SetViewModelContext(BuildCurrentContext());

    private void AplicarFiltro()
    {
        CxPItems.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t) ? _todas
            : _todas.Where(c => c.NombreAcreedor.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                 c.Concepto.Contains(t, StringComparison.OrdinalIgnoreCase));
        foreach (var c in lista) CxPItems.Add(c);
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
            Module: "Finanzas", SubModule: "CxP", ViewModel: nameof(CxPViewModel),
            Entity: "finanzas.CuentaPorPagar",
            RecordCount: CxPItems.Count, Duration: duration,
            EmpresaFilter:    new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing, Session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected, Note: "CxP filtran por empresa"),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "No aplica"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false (global)"),
            SearchTerm: string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos: SoloVigentes, CategoriaFiltro: null, FiltroTemporal: FiltroTemporal,
            Notes: denied ? [$"ACCESO DENEGADO — permiso requerido: {PermisosClave.CxP.Ver}"]
                          : [$"SoloVigentes={SoloVigentes} | Empresa={Session.EmpresaId}"],
            Timestamp: DateTime.Now));
    }

    private CurrentOperationalContext BuildCurrentContext(bool denied = false) =>
        new(Module: "Finanzas", SubModule: "CxP", ViewModel: nameof(CxPViewModel),
            Entity: "finanzas.CuentaPorPagar",
            RecordCount: CxPItems.Count,
            SearchTerm: string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos: SoloVigentes,
            CategoriaFiltro: denied ? $"DENEGADO ({PermisosClave.CxP.Ver})" : null,
            FiltroTemporal: FiltroTemporal,
            EmpresaFilter:    new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing, Session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected, Note: "Finanzas Empresa"),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "No aplica"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            Source: "ModuleFrame", UpdatedAt: DateTime.Now);
}

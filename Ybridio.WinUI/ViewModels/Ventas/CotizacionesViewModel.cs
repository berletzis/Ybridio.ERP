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

/// <summary>
/// ViewModel del sub-módulo Ventas > Cotizaciones.
/// </summary>
public sealed partial class CotizacionesViewModel : BaseContextViewModel
{
    private readonly ICotizacionService               _service;
    private readonly IErpAuthorizationService         _auth;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;

    [ObservableProperty] private bool                  isBusy;
    [ObservableProperty] private string                errorMessage   = string.Empty;
    [ObservableProperty] private string                successMessage = string.Empty;
    [ObservableProperty] private string                busqueda       = string.Empty;
    [ObservableProperty] private string                filtroTemporal = "30 días";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EliminarCommand))]
    [NotifyCanExecuteChangedFor(nameof(CambiarEstatusCommand))]
    private CotizacionResumenDto? cotizacionSeleccionada;

    // ── Document Surface UX Pattern ─────────────────────────────────────────
    /// <summary>
    /// Indica si el Document Surface está visible (true = oculta el grid, false = muestra el grid).
    /// Cuando está activo, el grid de listado se oculta y el Document Surface toma control del módulo.
    /// </summary>
    [ObservableProperty] private bool isDocumentSurfaceVisible;

    /// <summary>
    /// Contenido del Document Surface (instancia de CotizacionDocumentoPage o null).
    /// Este contenido reemplaza temporalmente el grid de listado.
    /// </summary>
    [ObservableProperty] private object? documentSurfaceContent;

    private bool _isRefreshing;

    private IReadOnlyList<CotizacionResumenDto> _todas = [];
    public ObservableCollection<CotizacionResumenDto> Cotizaciones { get; } = [];
    public Visibility IsEmptyState => Cotizaciones.Count == 0 && !IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public Action<EstatusCotizacion>? SolicitarCambiarEstatus;

    public CotizacionesViewModel(
        ICotizacionService               service,
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
        Cotizaciones.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyState));
    }

    private bool HaySeleccion => CotizacionSeleccionada is not null;

    partial void OnBusquedaChanged(string value)       => AplicarFiltro();
    partial void OnFiltroTemporalChanged(string value) => _ = RefrescarAsync();
    partial void OnIsBusyChanged(bool value)           => OnPropertyChanged(nameof(IsEmptyState));

    [RelayCommand(CanExecute = nameof(HaySeleccion))] private void CambiarEstatus()
        => SolicitarCambiarEstatus?.Invoke(CotizacionSeleccionada!.Estatus);

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (CotizacionSeleccionada is null || Session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.EliminarAsync(CotizacionSeleccionada.Id, Session.Usuario.Id, ct);
            if (r.Success) { SuccessMessage = "Cotización eliminada."; await RefrescarAsync(ct); }
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
            if (!await _auth.PuedeAsync(PermisosClave.Cotizacion.Ver, ct))
            {
                sw.Stop(); ErrorMessage = "Sin permiso para ver cotizaciones (cotizacion.ver).";
                ReportContext(sw.Elapsed, denied: true); return;
            }
            var (desde, hasta) = ParseFiltroTemporal();
            var result = await _service.ListarAsync(Session.EmpresaId, desde, hasta, ct);
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

    public Task<ServiceResult> CambiarEstatusOp(long id, EstatusCotizacion estatus, CancellationToken ct = default)
        => Session.Usuario is null
            ? Task.FromResult(ServiceResult.Fail("Sin sesión."))
            : _service.CambiarEstatusAsync(id, estatus, Session.Usuario.Id, ct);

    // ── Document Surface UX Pattern Commands ────────────────────────────────

    /// <summary>
    /// Abre el Document Surface para crear una nueva cotización.
    /// Reemplaza temporalmente el grid de listado.
    /// </summary>
    public void AbrirNuevaCotizacion()
    {
        DocumentSurfaceContent = null; // Preparar para nueva instancia
        IsDocumentSurfaceVisible = true;
    }

    /// <summary>
    /// Abre el Document Surface para editar una cotización existente.
    /// Reemplaza temporalmente el grid de listado.
    /// </summary>
    public void AbrirEditarCotizacion(CotizacionDto cotizacion)
    {
        DocumentSurfaceContent = cotizacion;
        IsDocumentSurfaceVisible = true;
    }

    /// <summary>
    /// Cierra el Document Surface y vuelve al grid de listado.
    /// Debe ser llamado después de guardar o cuando el usuario hace clic en "← Volver a Lista".
    /// </summary>
    public async Task CerrarDocumentSurfaceAsync()
    {
        IsDocumentSurfaceVisible = false;
        DocumentSurfaceContent = null;
        await RefrescarAsync(); // Refrescar grid después de cerrar
    }

    protected override Task OnContextChangedAsync() => RefrescarAsync();
    public void ReportLiveContext() => _contextTracker.SetViewModelContext(BuildCurrentContext());

    private void AplicarFiltro()
    {
        Cotizaciones.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t) ? _todas
            : _todas.Where(c => c.NombreCliente.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                 c.EstatusTexto.Contains(t, StringComparison.OrdinalIgnoreCase));
        foreach (var c in lista) Cotizaciones.Add(c);
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
            Module: "Ventas", SubModule: "Cotizaciones", ViewModel: nameof(CotizacionesViewModel),
            Entity: "ventas.Cotizacion", RecordCount: Cotizaciones.Count, Duration: duration,
            EmpresaFilter:    new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing, Session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected, Note: "Cotizaciones filtran por empresa"),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "No aplica"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            SearchTerm: string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos: false, CategoriaFiltro: null, FiltroTemporal: FiltroTemporal,
            Notes: denied ? [$"ACCESO DENEGADO — {PermisosClave.Cotizacion.Ver}"] : [$"Empresa={Session.EmpresaId}"],
            Timestamp: DateTime.Now));
    }

    private CurrentOperationalContext BuildCurrentContext(bool denied = false) =>
        new(Module: "Ventas", SubModule: "Cotizaciones", ViewModel: nameof(CotizacionesViewModel),
            Entity: "ventas.Cotizacion", RecordCount: Cotizaciones.Count,
            SearchTerm: string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos: false, CategoriaFiltro: denied ? $"DENEGADO ({PermisosClave.Cotizacion.Ver})" : null,
            FiltroTemporal: FiltroTemporal,
            EmpresaFilter:    new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing, Session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected, Note: "Cotizaciones por empresa"),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "No aplica"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            Source: "ModuleFrame", UpdatedAt: DateTime.Now);
}

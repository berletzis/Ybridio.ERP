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

/// <summary>ViewModel del sub-módulo Ventas > Pedidos.</summary>
public sealed partial class PedidosViewModel : BaseContextViewModel
{
    private readonly IPedidoService                   _service;
    private readonly IErpAuthorizationService         _auth;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;

    [ObservableProperty] private bool              isBusy;
    [ObservableProperty] private string            errorMessage   = string.Empty;
    [ObservableProperty] private string            successMessage = string.Empty;
    [ObservableProperty] private string            busqueda       = string.Empty;
    [ObservableProperty] private string            filtroTemporal = "30 días";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EliminarCommand))]
    [NotifyCanExecuteChangedFor(nameof(AvanzarEstatusCommand))]
    private PedidoResumenDto? pedidoSeleccionado;

    // ── Document Surface UX Pattern (ADR-025 + ADR-030 + ADR-031) ────────────
    private bool    _isDocumentSurfaceVisible;
    private object? _documentSurfaceContent;
    private bool    _isDocumentSurfaceDetached;

    /// <summary>Indica si el Document Surface está visible (reemplaza el listado).</summary>
    public bool IsDocumentSurfaceVisible
    {
        get => _isDocumentSurfaceVisible;
        set => SetProperty(ref _isDocumentSurfaceVisible, value);
    }

    /// <summary>Contenido actual del Document Surface (página de documento).</summary>
    public object? DocumentSurfaceContent
    {
        get => _documentSurfaceContent;
        set => SetProperty(ref _documentSurfaceContent, value);
    }

    /// <summary>Indica si el surface está en modo split/detached.</summary>
    public bool IsDocumentSurfaceDetached
    {
        get => _isDocumentSurfaceDetached;
        set => SetProperty(ref _isDocumentSurfaceDetached, value);
    }

    /// <summary>Abre el Document Surface para un nuevo pedido.</summary>
    public void AbrirNuevoPedido(object page)
    {
        DocumentSurfaceContent    = page;
        IsDocumentSurfaceVisible  = true;
        IsDocumentSurfaceDetached = false;
    }

    /// <summary>Abre el Document Surface para editar un pedido existente.</summary>
    public void AbrirEditarPedido(object page)
    {
        DocumentSurfaceContent    = page;
        IsDocumentSurfaceVisible  = true;
        IsDocumentSurfaceDetached = false;
    }

    /// <summary>Cierra el Document Surface y vuelve al listado.</summary>
    public async Task CerrarDocumentSurfaceAsync()
    {
        IsDocumentSurfaceVisible  = false;
        IsDocumentSurfaceDetached = false;
        DocumentSurfaceContent    = null;
        await RefrescarCommand.ExecuteAsync(null);
    }

    /// <summary>Alterna el modo split/detached del Document Surface.</summary>
    public void ToggleDetach()
    {
        if (!IsDocumentSurfaceVisible) return;
        IsDocumentSurfaceDetached = !IsDocumentSurfaceDetached;
    }
    // ─────────────────────────────────────────────────────────────────────────

    private bool _isRefreshing;

    private IReadOnlyList<PedidoResumenDto> _todos = [];
    public ObservableCollection<PedidoResumenDto> Pedidos { get; } = [];
    public Visibility IsEmptyState => Pedidos.Count == 0 && !IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public PedidosViewModel(
        IPedidoService                   service,
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
        Pedidos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyState));
    }

    private bool HaySeleccion    => PedidoSeleccionado is not null;
    private bool PuedeAvanzar    => PedidoSeleccionado is { Estatus: not EstatusPedido.Completado and not EstatusPedido.Cancelado };

    partial void OnBusquedaChanged(string value)       => AplicarFiltro();
    partial void OnFiltroTemporalChanged(string value) => _ = RefrescarAsync();
    partial void OnIsBusyChanged(bool value)           => OnPropertyChanged(nameof(IsEmptyState));

    [RelayCommand(CanExecute = nameof(PuedeAvanzar))]
    private async Task AvanzarEstatusAsync(CancellationToken ct = default)
    {
        if (PedidoSeleccionado is null || Session.Usuario is null) return;
        var siguiente = PedidoSeleccionado.Estatus switch
        {
            EstatusPedido.Nuevo      => EstatusPedido.Confirmado,
            EstatusPedido.Confirmado => EstatusPedido.EnProceso,
            EstatusPedido.EnProceso  => EstatusPedido.Completado,
            _                        => PedidoSeleccionado.Estatus
        };
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.CambiarEstatusAsync(PedidoSeleccionado.Id, siguiente, Session.Usuario.Id, ct);
            if (r.Success) { SuccessMessage = $"Pedido → {siguiente}."; await RefrescarAsync(ct); }
            else           { ErrorMessage   = r.Error ?? "No se pudo avanzar."; }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task EliminarAsync(CancellationToken ct = default)
    {
        if (PedidoSeleccionado is null || Session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.EliminarAsync(PedidoSeleccionado.Id, Session.Usuario.Id, ct);
            if (r.Success) { SuccessMessage = "Pedido eliminado."; await RefrescarAsync(ct); }
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
            if (!await _auth.PuedeAsync(PermisosClave.Pedido.Ver, ct))
            {
                sw.Stop(); ErrorMessage = "Sin permiso (pedido.ver).";
                ReportContext(sw.Elapsed, denied: true); return;
            }
            var (desde, hasta) = ParseFiltroTemporal();
            var result = await _service.ListarAsync(Session.EmpresaId, desde, hasta, ct);
            if (!result.Success) { sw.Stop(); ErrorMessage = result.Error ?? "Error."; ReportContext(sw.Elapsed, denied: result.ErrorCode == ErrorCode.Unauthorized); return; }
            _todos = result.Value!;
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

    protected override Task OnContextChangedAsync() => RefrescarAsync();
    public void ReportLiveContext() => _contextTracker.SetViewModelContext(BuildCurrentContext());

    private void AplicarFiltro()
    {
        Pedidos.Clear();
        var t = Busqueda.Trim();
        var lista = string.IsNullOrWhiteSpace(t) ? _todos
            : _todos.Where(p => p.NombreCliente.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                 p.EstatusTexto.Contains(t, StringComparison.OrdinalIgnoreCase));
        foreach (var p in lista) Pedidos.Add(p);
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
            Module: "Ventas", SubModule: "Pedidos", ViewModel: nameof(PedidosViewModel),
            Entity: "ventas.Pedido", RecordCount: Pedidos.Count, Duration: duration,
            EmpresaFilter:    new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing, Session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected, Note: "Pedidos por empresa"),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "No aplica"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            SearchTerm: string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos: false, CategoriaFiltro: null, FiltroTemporal: FiltroTemporal,
            Notes: denied ? [$"ACCESO DENEGADO — {PermisosClave.Pedido.Ver}"] : [$"Empresa={Session.EmpresaId}"],
            Timestamp: DateTime.Now));
    }

    private CurrentOperationalContext BuildCurrentContext(bool denied = false) =>
        new(Module: "Ventas", SubModule: "Pedidos", ViewModel: nameof(PedidosViewModel),
            Entity: "ventas.Pedido", RecordCount: Pedidos.Count,
            SearchTerm: string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda,
            SoloActivos: false, CategoriaFiltro: denied ? $"DENEGADO ({PermisosClave.Pedido.Ver})" : null,
            FiltroTemporal: FiltroTemporal,
            EmpresaFilter:    new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing, Session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected, Note: "Pedidos por empresa"),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "No aplica"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            Source: "ModuleFrame", UpdatedAt: DateTime.Now);
}

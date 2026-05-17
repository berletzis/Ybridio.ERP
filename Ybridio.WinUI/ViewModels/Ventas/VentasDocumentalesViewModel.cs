using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Application.Services.Venta;
using Ybridio.Domain.Ventas;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels;
using Ybridio.WinUI.Views.Ventas;

namespace Ybridio.WinUI.ViewModels.Ventas;

/// <summary>ViewModel del sub-módulo Ventas > Ventas (lista de ventas documentales).</summary>
public sealed partial class VentasDocumentalesViewModel : BaseContextViewModel
{
    private readonly IVentaDocumentalService          _service;
    private readonly IErpAuthorizationService         _auth;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;

    [ObservableProperty] private bool                         _isBusy;
    [ObservableProperty] private string                       _errorMessage   = string.Empty;
    [ObservableProperty] private string                       _successMessage = string.Empty;
    [ObservableProperty] private string                       _busqueda       = string.Empty;
    [ObservableProperty] private string                       _filtroTemporal = "30 dias";
    [ObservableProperty] private string                       _filtroEstatus  = "Todas";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AbrirDetalleCommand))]
    private VentaDocumentalResumenDto? _ventaSeleccionada;

    private IReadOnlyList<VentaDocumentalResumenDto> _todasLasVentas = [];

    /// <summary>Suma de Total de todas las ventas cerradas visibles en el grid.</summary>
    [ObservableProperty] private decimal _totalAcumulado;

    /// <summary>Cantidad de documentos visibles en el grid (proxy de registros, pues ResumenDto no expone líneas).</summary>
    [ObservableProperty] private int _totalProductosDistintos;

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

    /// <summary>Guard para AbrirDetalleCommand: requiere una venta seleccionada en el grid.</summary>
    public bool HaySeleccion => VentaSeleccionada is not null;

    private long? _currentSurfaceVentaId;

    /// <summary>
    /// Abre el Document Surface inline con la venta seleccionada en modo lectura completo.
    /// Aplica Single Document Session Rule (ADR): no reabre si el mismo documento ya está activo.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HaySeleccion))]
    private async Task AbrirDetalleAsync(CancellationToken ct = default)
    {
        if (VentaSeleccionada is null) return;

        // Single Document Session Rule — no reabrir el mismo documento
        if (IsDocumentSurfaceVisible && _currentSurfaceVentaId == VentaSeleccionada.Id) return;

        IsBusy = true; ErrorMessage = string.Empty;
        try
        {
            var r = await _service.ObtenerConDetallesAsync(VentaSeleccionada.Id, ct);
            if (!r.Success)
            {
                ErrorMessage = r.Error ?? "No se pudo cargar la venta.";
                return;
            }

            var page = new VentaDocumentoPage(r.Value);
            page.OnCerrar = async () => await CerrarDocumentSurfaceAsync();

            _currentSurfaceVentaId = VentaSeleccionada.Id;
            AbrirDocumentoVenta(page);
        }
        catch (TaskCanceledException)
        {
            // ADR-026: expected during navigation/lifecycle transitions.
        }
        finally { IsBusy = false; }
    }

    /// <summary>Abre el Document Surface con una página de Venta.</summary>
    public void AbrirDocumentoVenta(object page)
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
        _currentSurfaceVentaId    = null;
        try
        {
            await RefrescarCommand.ExecuteAsync(null);
        }
        catch (TaskCanceledException)
        {
            // ADR-026: expected during rapid close/navigation lifecycle transitions.
        }
    }

    /// <summary>Alterna el modo split/detached del Document Surface.</summary>
    public void ToggleDetach()
    {
        if (!IsDocumentSurfaceVisible) return;
        IsDocumentSurfaceDetached = !IsDocumentSurfaceDetached;
    }
    // ─────────────────────────────────────────────────────────────────────────

    public ObservableCollection<VentaDocumentalResumenDto> Ventas { get; } = [];

    /// <summary>Estado vacío: sin ventas y sin carga en curso.</summary>
    public Visibility IsEmptyState => Ventas.Count == 0 && !IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public VentasDocumentalesViewModel(
        IVentaDocumentalService          service,
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

        Ventas.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyState));
    }

    partial void OnIsBusyChanged(bool value)       => OnPropertyChanged(nameof(IsEmptyState));
    partial void OnBusquedaChanged(string _)       => AplicarFiltro();
    partial void OnFiltroEstatusChanged(string _)  => AplicarFiltro();
    partial void OnFiltroTemporalChanged(string value) { _ = RefrescarAsync(); }

    protected override Task OnContextChangedAsync() => RefrescarAsync();

    /// <summary>Recarga todas las ventas documentales desde el servicio y aplica filtros locales.</summary>
    [RelayCommand]
    public async Task RefrescarAsync(CancellationToken ct = default)
    {
        if (Session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var dias = FiltroTemporal switch
            {
                "7 dias"  =>  7,
                "90 dias" => 90,
                "6 meses" => 180,
                "1 ano"   => 365,
                "Todo"    => 3650,
                _         => 30,
            };
            var desde = DateTime.Today.AddDays(-dias);
            var r = await _service.ListarAsync(Session.EmpresaId, desde, null, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "Error al cargar."; return; }
            _todasLasVentas = r.Value!;
            AplicarFiltro();
        }
        catch (TaskCanceledException)
        {
            // ADR-026: expected during navigation/lifecycle transitions.
        }
        finally { IsBusy = false; }
    }

    private void AplicarFiltro()
    {
        var lista = _todasLasVentas.AsEnumerable();

        lista = FiltroEstatus switch
        {
            "Borrador"       => lista.Where(v => v.Estatus == EstatusVenta.Borrador),
            "Pendiente Pago" => lista.Where(v => v.Estatus == EstatusVenta.PendientePago),
            "Pagada"         => lista.Where(v => v.Estatus == EstatusVenta.Pagada),
            "Cerrada"        => lista.Where(v => v.Estatus == EstatusVenta.Cerrada),
            "Cancelada"      => lista.Where(v => v.Estatus == EstatusVenta.Cancelada),
            _                => lista
        };

        if (!string.IsNullOrWhiteSpace(Busqueda))
            lista = lista.Where(v =>
                v.NombreCliente.Contains(Busqueda, StringComparison.OrdinalIgnoreCase) ||
                (v.Folio?.Contains(Busqueda, StringComparison.OrdinalIgnoreCase) ?? false) ||
                v.EstatusTexto.Contains(Busqueda, StringComparison.OrdinalIgnoreCase));

        Ventas.Clear();
        foreach (var v in lista) Ventas.Add(v);

        TotalAcumulado          = Ventas.Sum(v => v.Total);
        TotalProductosDistintos = Ventas.Count;
        ReportarContexto();
    }

    private void ReportarContexto()
    {
        _contextTracker.SetViewModelContext(new CurrentOperationalContext(
            Module:          "Ventas",
            SubModule:       "Ventas",
            ViewModel:       nameof(VentasDocumentalesViewModel),
            Entity:          "Venta",
            RecordCount:     Ventas.Count,
            SearchTerm:      Busqueda,
            SoloActivos:     false,
            CategoriaFiltro: null,
            FiltroTemporal:  FiltroTemporal,
            EmpresaFilter:   null,
            SucursalFilter:  null,
            AlmacenFilter:   null,
            SoftDeleteFilter: null,
            Source:          "WorkspaceTab",
            UpdatedAt:       DateTime.Now));
    }

    /// <summary>Reporta el contexto vivo (llamado por la Page al revisitar el tab).</summary>
    public void ReportLiveContext() => ReportarContexto();
}

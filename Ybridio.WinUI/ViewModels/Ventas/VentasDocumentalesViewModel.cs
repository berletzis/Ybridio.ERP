using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Application.Services.Venta;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels;

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
    [ObservableProperty] private VentaDocumentalResumenDto?   _ventaSeleccionada;

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

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsEmptyState));

    protected override Task OnContextChangedAsync() => RefrescarAsync();

    /// <summary>Recarga la lista de ventas documentales según el filtro temporal activo.</summary>
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
            Ventas.Clear();
            if (!r.Success) { ErrorMessage = r.Error ?? "Error al cargar."; return; }
            var filtradas = string.IsNullOrWhiteSpace(Busqueda)
                ? r.Value!
                : r.Value!.Where(v => v.NombreCliente.Contains(Busqueda, StringComparison.OrdinalIgnoreCase)
                                   || v.EstatusTexto.Contains(Busqueda, StringComparison.OrdinalIgnoreCase));
            foreach (var v in filtradas)
                Ventas.Add(v);

            ReportarContexto();
        }
        catch (TaskCanceledException)
        {
            // ADR-026: Expected during navigation/lifecycle transitions.
            // [RelayCommand] cancels previous invocation on re-entry or page unload.
            // Not an error — UX remains clean, no crash, no Debugger.Break.
        }
        finally { IsBusy = false; }
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

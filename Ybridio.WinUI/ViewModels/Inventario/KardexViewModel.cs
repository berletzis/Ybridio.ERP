using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System;
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
/// ViewModel del Kardex operacional.
/// Lista movimientos de inventario con filtros de producto, almacén, tipo y fechas.
/// Aplica enforcement runtime de permiso <c>kardex.ver</c> y scope de almacén.
/// </summary>
public sealed partial class KardexViewModel : BaseContextViewModel
{
    private readonly IInventarioService              _inventario;
    private readonly IErpAuthorizationService        _auth;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker          _contextTracker;

    // ── Estado de carga ───────────────────────────────────────────────────────
    [ObservableProperty] private bool   isBusy;
    [ObservableProperty] private string errorMessage   = string.Empty;
    [ObservableProperty] private string successMessage = string.Empty;

    private bool _isLoading;

    // ── Filtros ────────────────────────────────────────────────────────────────
    [ObservableProperty] private string busquedaProducto = string.Empty;
    [ObservableProperty] private int?   filtroAlmacenId;
    [ObservableProperty] private int?   filtroTipoMovimientoId;
    [ObservableProperty] private DateTimeOffset filtroDesde = DateTimeOffset.Now.AddDays(-30);
    [ObservableProperty] private DateTimeOffset filtroHasta = DateTimeOffset.Now;

    // ── Datos ─────────────────────────────────────────────────────────────────
    private System.Collections.Generic.IReadOnlyList<KardexLineaDto> _todas = [];

    /// <summary>Colección de líneas de kardex mostradas en el grid.</summary>
    public ObservableCollection<KardexLineaDto> Movimientos { get; } = [];

    /// <summary>Kardex seleccionado actualmente en el grid.</summary>
    [ObservableProperty] private KardexLineaDto? lineaSeleccionada;

    public Visibility IsEmptyState =>
        Movimientos.Count == 0 && !IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public KardexViewModel(
        IInventarioService               inventario,
        IErpAuthorizationService         auth,
        SessionService                   session,
        IOperationalObservabilityService observability,
        ICurrentContextTracker           contextTracker)
        : base(session)
    {
        _inventario     = inventario;
        _auth           = auth;
        _observability  = observability;
        _contextTracker = contextTracker;
        Movimientos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyState));
    }

    partial void OnIsBusyChanged(bool _) => OnPropertyChanged(nameof(IsEmptyState));
    partial void OnBusquedaProductoChanged(string _) => AplicarFiltro();

    // ── Comando principal de carga ────────────────────────────────────────────

    /// <summary>Carga el kardex aplicando los filtros activos y enforcement de seguridad.</summary>
    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (_isLoading) return;
        if (Session.EmpresaId == 0) return;

        _isLoading = true;

        IsBusy        = true;
        ErrorMessage  = string.Empty;
        SuccessMessage = string.Empty;
        var sw = Stopwatch.StartNew();

        try
        {
            // ── Permiso ────────────────────────────────────────────────────────
            if (!await _auth.PuedeAsync(PermisosClave.Kardex.Ver, ct))
            {
                sw.Stop();
                ErrorMessage = "Sin permiso para ver el kardex (kardex.ver).";
                _observability.Report(BuildOperationalContext(sw.Elapsed, denied: true));
                _contextTracker.SetViewModelContext(BuildCurrentContext(denied: true));
                return;
            }

            var desde = FiltroDesde.DateTime;
            var hasta = FiltroHasta.DateTime.Date.AddDays(1).AddSeconds(-1);

            var result = await _inventario.ListarKardexFiltradoAsync(
                empresaId:       Session.EmpresaId,
                productoId:      null,
                almacenId:       FiltroAlmacenId,
                tipoMovimientoId: FiltroTipoMovimientoId,
                desde:           desde,
                hasta:           hasta,
                ct:              ct);

            if (!result.Success)
            {
                sw.Stop();
                ErrorMessage = result.Error ?? "Error al cargar el kardex.";
                _observability.Report(BuildOperationalContext(sw.Elapsed,
                    denied: result.ErrorCode == ErrorCode.Unauthorized));
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
            _isLoading = false;
        }
    }

    /// <summary>Limpia los filtros y recarga.</summary>
    [RelayCommand]
    public async Task LimpiarFiltrosAsync(CancellationToken ct = default)
    {
        BusquedaProducto     = string.Empty;
        FiltroAlmacenId      = null;
        FiltroTipoMovimientoId = null;
        FiltroDesde          = DateTimeOffset.Now.AddDays(-30);
        FiltroHasta          = DateTimeOffset.Now;
        await LoadAsync(ct);
    }

    /// <summary>Inicia la navegación hacia el documento origen de la línea seleccionada.</summary>
    [RelayCommand(CanExecute = nameof(PuedeNavegar))]
    public void AbrirDocumentoOrigen()
    {
        if (LineaSeleccionada is null) return;
        // La navegación cruzada se delega al evento de la View; aquí solo exponemos el modelo.
        DocumentoOrigenSolicitado?.Invoke(LineaSeleccionada);
    }

    private bool PuedeNavegar() => LineaSeleccionada?.ReferenciaId is not null;

    partial void OnLineaSeleccionadaChanged(KardexLineaDto? _)
        => AbrirDocumentoOrigenCommand.NotifyCanExecuteChanged();

    /// <summary>
    /// Evento que la View puede suscribir para abrir el documento origen.
    /// Expone la línea de kardex seleccionada con el <see cref="KardexLineaDto.ReferenciaId"/> y tipo.
    /// </summary>
    public event Action<KardexLineaDto>? DocumentoOrigenSolicitado;

    protected override Task OnContextChangedAsync() => LoadAsync();

    public void ReportLiveContext()
        => _contextTracker.SetViewModelContext(BuildCurrentContext());

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void AplicarFiltro()
    {
        Movimientos.Clear();
        var t = BusquedaProducto.Trim();
        var lista = string.IsNullOrWhiteSpace(t)
            ? _todas
            : _todas.Where(m =>
                m.ProductoNombre.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                m.ProductoCodigo.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                m.Referencia?.Contains(t, StringComparison.OrdinalIgnoreCase) == true);
        foreach (var m in lista) Movimientos.Add(m);
        OnPropertyChanged(nameof(IsEmptyState));
    }

    private GridOperationContext BuildOperationalContext(TimeSpan duration, bool denied = false) =>
        new(
            Module:          "Inventario",
            SubModule:       "Kardex",
            ViewModel:       nameof(KardexViewModel),
            Entity:          "inventario.MovimientoInventario",
            RecordCount:     Movimientos.Count,
            Duration:        duration,
            EmpresaFilter:   new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing,
                                 Session.EmpresaId.ToString()),
            SucursalFilter:  new(FilterState.OmittedExpected,
                                 Note: "Kardex filtra por almacén vía scope"),
            AlmacenFilter:   new(FiltroAlmacenId.HasValue ? FilterState.Applied : FilterState.OmittedExpected,
                                 FiltroAlmacenId?.ToString()),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            SearchTerm:      string.IsNullOrWhiteSpace(BusquedaProducto) ? null : BusquedaProducto,
            SoloActivos:     false,
            CategoriaFiltro: FiltroTipoMovimientoId?.ToString(),
            FiltroTemporal:  $"{FiltroDesde:d} – {FiltroHasta:d}",
            Notes:           denied
                               ? [$"ACCESO DENEGADO — permiso requerido: {PermisosClave.Kardex.Ver}"]
                               : [$"Empresa={Session.EmpresaId}", $"Scope almacén aplicado"],
            Timestamp:       DateTime.Now);

    private CurrentOperationalContext BuildCurrentContext(bool denied = false) =>
        new(
            Module:          "Inventario",
            SubModule:       "Kardex",
            ViewModel:       nameof(KardexViewModel),
            Entity:          "inventario.MovimientoInventario",
            RecordCount:     Movimientos.Count,
            SearchTerm:      string.IsNullOrWhiteSpace(BusquedaProducto) ? null : BusquedaProducto,
            SoloActivos:     false,
            CategoriaFiltro: denied
                               ? $"DENEGADO ({PermisosClave.Kardex.Ver})"
                               : FiltroTipoMovimientoId?.ToString(),
            FiltroTemporal:  $"{FiltroDesde:d} – {FiltroHasta:d}",
            EmpresaFilter:   new(Session.EmpresaId != 0 ? FilterState.Applied : FilterState.Missing,
                                 Session.EmpresaId.ToString()),
            SucursalFilter:  new(FilterState.OmittedExpected, Note: "Kardex filtra por almacén"),
            AlmacenFilter:   new(FiltroAlmacenId.HasValue ? FilterState.Applied : FilterState.OmittedExpected,
                                 FiltroAlmacenId?.ToString()),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            Source:          "ModuleFrame",
            UpdatedAt:       DateTime.Now);
}

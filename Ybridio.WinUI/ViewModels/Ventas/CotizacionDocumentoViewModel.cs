using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
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

namespace Ybridio.WinUI.ViewModels.Ventas;

/// <summary>
/// ViewModel de documento para el formulario de cotización (nueva o existente).
/// Gestiona el encabezado y las líneas de detalle de la cotización.
///
/// <para>Para documentos NUEVOS: los detalles se acumulan en memoria hasta "Guardar"
/// que llama a CrearAsync con todos los datos en una sola operación.</para>
///
/// <para>Para documentos EXISTENTES: agregar/quitar detalles es inmediato (persiste
/// al servicio). El encabezado se actualiza con "Guardar" via ActualizarAsync.</para>
/// </summary>
/// <remarks>
/// Fórmulas:
/// - DetalleLinea.Importe = Cantidad × PrecioUnitario (calculado, persistido en BD)
/// - Subtotal = SUM(Detalles.Importe) — runtime, se persiste en BD al guardar
/// - Total = Subtotal — V1 sin IVA independiente
/// </remarks>
public sealed partial class CotizacionDocumentoViewModel : ObservableObject
{
    private readonly ICotizacionService               _service;
    private readonly IErpAuthorizationService         _auth;
    private readonly SessionService                   _session;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;

    // El documento cargado (null si es nuevo)
    private CotizacionDto? _documento;

    // ── Estado del documento ─────────────────────────────────────────────────
    [ObservableProperty] private bool              isNuevo = true;
    [ObservableProperty] private bool              isBusy;
    [ObservableProperty] private string            errorMessage   = string.Empty;
    [ObservableProperty] private string            successMessage = string.Empty;

    // ── Encabezado (editable) ────────────────────────────────────────────────
    [ObservableProperty] private string            nombreCliente  = string.Empty;
    [ObservableProperty] private int?              clienteId;
    [ObservableProperty] private DateTime          fecha          = DateTime.Today;
    [ObservableProperty] private DateTime?         fechaVigencia;

    /// <summary>Wrapper DateTimeOffset para el DatePicker de Fecha.</summary>
    public DateTimeOffset FechaOffset
    {
        get => new DateTimeOffset(Fecha);
        set => Fecha = value.DateTime;
    }

    /// <summary>Wrapper DateTimeOffset? para el DatePicker de FechaVigencia.</summary>
    public DateTimeOffset FechaVigenciaOffset
    {
        get => FechaVigencia.HasValue ? new DateTimeOffset(FechaVigencia.Value) : DateTimeOffset.Now.AddDays(30);
        set => FechaVigencia = value.DateTime;
    }

    partial void OnFechaChanged(DateTime value)          => OnPropertyChanged(nameof(FechaOffset));
    partial void OnFechaVigenciaChanged(DateTime? value) => OnPropertyChanged(nameof(FechaVigenciaOffset));
    [ObservableProperty] private string?           observaciones;
    [ObservableProperty] private EstatusCotizacion estatus        = EstatusCotizacion.Borrador;

    // ── Detalles ─────────────────────────────────────────────────────────────
    public ObservableCollection<DetalleLineaEditable> Detalles { get; } = [];

    // ── Totales (runtime, recalculados cuando cambian los detalles) ─────────

    /// <summary>
    /// Subtotal = SUM(Detalles.Importe)
    /// Calculado runtime en el ViewModel; se persiste en BD al llamar Guardar.
    /// </summary>
    [ObservableProperty] private decimal subtotal;

    /// <summary>
    /// Total = Subtotal (V1 sin IVA independiente).
    /// Se persistirá en BD al llamar Guardar.
    /// </summary>
    [ObservableProperty] private decimal total;

    // ── UI helpers ───────────────────────────────────────────────────────────
    public string TituloDocumento => IsNuevo ? "Nueva Cotización" : $"Cotización #{_documento?.Id}";
    public string EstatusTextoDisplay => Estatus switch
    {
        EstatusCotizacion.Borrador  => "Borrador",
        EstatusCotizacion.Enviada   => "Enviada",
        EstatusCotizacion.Aprobada  => "Aprobada",
        EstatusCotizacion.Cancelada => "Cancelada",
        _                           => Estatus.ToString()
    };
    public bool PuedeEditar       => Estatus != EstatusCotizacion.Cancelada;
    public bool PuedeEnviar       => Estatus == EstatusCotizacion.Borrador;
    public bool PuedeAprobar      => Estatus == EstatusCotizacion.Enviada;
    public bool PuedeConvertir    => Estatus == EstatusCotizacion.Aprobada && !IsNuevo;
    public bool PuedeCancelar     => Estatus is EstatusCotizacion.Borrador or EstatusCotizacion.Enviada && !IsNuevo;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EliminarDetalleCommand))]
    private DetalleLineaEditable? detalleSeleccionado;

    // Callbacks para acciones que requieren XamlRoot (abrirán dialogs o workspace)
    public Action<DetalleLineaEditable?>? SolicitarNuevoEditar;
    public Func<Task<DetalleLineaEditable?>>? SolicitarAgregarDetalle;
    public Action<PedidoDto>?               NotificarPedidoGenerado;

    public CotizacionDocumentoViewModel(
        ICotizacionService               service,
        IErpAuthorizationService         auth,
        SessionService                   session,
        IOperationalObservabilityService observability,
        ICurrentContextTracker           contextTracker)
    {
        _service        = service;
        _auth           = auth;
        _session        = session;
        _observability  = observability;
        _contextTracker = contextTracker;
        Detalles.CollectionChanged += (_, _) => RecalcularTotales();
    }

    /// <summary>Inicializa el ViewModel con una cotización existente o deja en blanco para nueva.</summary>
    public void Initialize(CotizacionDto? cotizacion)
    {
        _documento = cotizacion;
        IsNuevo    = cotizacion is null;

        if (cotizacion is not null)
        {
            NombreCliente = cotizacion.NombreCliente;
            ClienteId     = cotizacion.ClienteId;
            Fecha         = cotizacion.Fecha;
            FechaVigencia = cotizacion.FechaVigencia;
            Observaciones = cotizacion.Observaciones;
            Estatus       = cotizacion.Estatus;

            Detalles.Clear();
            foreach (var d in cotizacion.Detalles)
                Detalles.Add(new DetalleLineaEditable(d.Id, d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario));
        }

        RecalcularTotales();
        OnPropertyChanged(nameof(TituloDocumento));
        OnPropertyChanged(nameof(EstatusTextoDisplay));
        RefrescarPermisosUI();
    }

    // ── Comandos ─────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task GuardarAsync(CancellationToken ct = default)
    {
        if (_session.Usuario is null) return;
        if (string.IsNullOrWhiteSpace(NombreCliente)) { ErrorMessage = "El nombre del cliente es obligatorio."; return; }

        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            if (IsNuevo)
            {
                // NEW document: create everything in one call
                var dto = new CrearCotizacionDto(
                    _session.EmpresaId, _session.SucursalId != 0 ? _session.SucursalId : null,
                    ClienteId, NombreCliente, Fecha, FechaVigencia, Observaciones,
                    Detalles.Select(d => new CrearDetalleLineaDto(d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario)).ToList());

                var r = await _service.CrearAsync(dto, _session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo crear."; return; }
                SuccessMessage = $"Cotización #{r.Value!.Id} creada.";
                Initialize(r.Value);
            }
            else
            {
                // EXISTING document: update header only
                var dto = new ActualizarCotizacionDto(ClienteId, NombreCliente, Fecha, FechaVigencia, Observaciones);
                var r   = await _service.ActualizarAsync(_documento!.Id, dto, _session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo actualizar."; return; }
                SuccessMessage = "Cotización actualizada.";
                _documento = r.Value;
            }
            ReportarContexto();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task AgregarDetalleAsync(CancellationToken ct = default)
    {
        if (_session.Usuario is null) return;
        // For existing docs: calls service immediately
        // For new docs: the callback will add to local collection
        if (!IsNuevo && _documento is not null)
        {
            // Delegate to page via callback for dialog, then add via service
            // The page will provide SolicitarAgregarDetalle which shows a dialog
            // and returns the filled DetalleLineaEditable
            return;
        }
        // For new docs: the page will handle this via SolicitarAgregarDetalle callback
    }

    public async Task<bool> AgregarDetalleLocalAsync(DetalleLineaEditable detalle)
    {
        if (IsNuevo)
        {
            // New doc: add to local collection
            Detalles.Add(detalle);
            return true;
        }
        else
        {
            // Existing doc: persist immediately
            if (_session.Usuario is null) return false;
            IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
            try
            {
                var dto = new CrearDetalleLineaDto(detalle.ProductoId, detalle.Descripcion, detalle.Cantidad, detalle.PrecioUnitario);
                var r   = await _service.AgregarDetalleAsync(_documento!.Id, dto, _session.Usuario.Id);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo agregar."; return false; }
                Detalles.Add(new DetalleLineaEditable(r.Value!.Id, r.Value.ProductoId, r.Value.Descripcion, r.Value.Cantidad, r.Value.PrecioUnitario));
                RecalcularTotales();
                return true;
            }
            finally { IsBusy = false; }
        }
    }

    private bool HayDetalleSeleccionado => DetalleSeleccionado is not null;

    [RelayCommand(CanExecute = nameof(HayDetalleSeleccionado))]
    public async Task EliminarDetalleAsync(CancellationToken ct = default)
    {
        if (DetalleSeleccionado is null) return;
        if (IsNuevo)
        {
            Detalles.Remove(DetalleSeleccionado);
            DetalleSeleccionado = null;
            return;
        }
        if (_session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.EliminarDetalleAsync(DetalleSeleccionado.Id, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo eliminar."; return; }
            Detalles.Remove(DetalleSeleccionado);
            DetalleSeleccionado = null;
            RecalcularTotales();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task CambiarEstatusAsync(EstatusCotizacion nuevoEstatus, CancellationToken ct = default)
    {
        if (_documento is null || _session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.CambiarEstatusAsync(_documento.Id, nuevoEstatus, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo cambiar el estado."; return; }
            Estatus = nuevoEstatus;
            _documento = _documento with { Estatus = nuevoEstatus };
            SuccessMessage = $"Estado actualizado → {EstatusTextoDisplay}";
            RefrescarPermisosUI();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task ConvertirAPedidoAsync(CancellationToken ct = default)
    {
        if (_documento is null || _session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.ConvertirAPedidoAsync(_documento.Id, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo convertir."; return; }
            SuccessMessage = $"Pedido #{r.Value!.Id} generado.";
            NotificarPedidoGenerado?.Invoke(r.Value);
        }
        finally { IsBusy = false; }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void RecalcularTotales()
    {
        // Subtotal = SUM(Detalles.Importe)  — runtime, se persiste al guardar
        Subtotal = Detalles.Sum(d => d.Importe);
        // Total = Subtotal  (V1: sin IVA independiente)
        Total = Subtotal;
    }

    private void RefrescarPermisosUI()
    {
        OnPropertyChanged(nameof(PuedeEditar));
        OnPropertyChanged(nameof(PuedeEnviar));
        OnPropertyChanged(nameof(PuedeAprobar));
        OnPropertyChanged(nameof(PuedeConvertir));
        OnPropertyChanged(nameof(PuedeCancelar));
        OnPropertyChanged(nameof(EstatusTextoDisplay));
    }

    private void ReportarContexto()
    {
        _contextTracker.SetViewModelContext(new CurrentOperationalContext(
            Module: "Ventas", SubModule: "Cotizaciones",
            ViewModel: nameof(CotizacionDocumentoViewModel),
            Entity: "ventas.Cotizacion",
            RecordCount: Detalles.Count,
            SearchTerm: null, SoloActivos: false,
            CategoriaFiltro: IsNuevo ? "NUEVA" : $"#{_documento?.Id}",
            FiltroTemporal: null,
            EmpresaFilter:    new(FilterState.Applied, _session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected, Note: "Cotizaciones por empresa"),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "No aplica"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            Source: "WorkspaceTab", UpdatedAt: DateTime.Now));
    }
}

/// <summary>
/// Línea de detalle editable en memoria para el documento.
/// Id = 0 para líneas nuevas no guardadas; > 0 para líneas ya persistidas.
/// </summary>
/// <remarks>
/// Importe = Cantidad × PrecioUnitario — runtime, NO persistido directamente aquí.
/// Se persiste en BD cuando el servicio llama a SaveChangesAsync.
/// </remarks>
public sealed class DetalleLineaEditable
{
    public DetalleLineaEditable() { }
    public DetalleLineaEditable(long id, int? productoId, string descripcion, decimal cantidad, decimal precioUnitario)
    {
        Id             = id;
        ProductoId     = productoId;
        Descripcion    = descripcion;
        Cantidad       = cantidad;
        PrecioUnitario = precioUnitario;
    }

    public long    Id             { get; set; }
    public int?    ProductoId     { get; set; }
    public string  Descripcion    { get; set; } = string.Empty;
    public decimal Cantidad       { get; set; }
    public decimal PrecioUnitario { get; set; }

    /// <summary>
    /// Importe = Cantidad × PrecioUnitario.
    /// Calculado runtime — no se almacena directamente en esta clase.
    /// El servicio lo persiste en BD al guardar.
    /// </summary>
    public decimal Importe => Cantidad * PrecioUnitario;
}

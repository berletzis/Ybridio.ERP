using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Directorio;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Application.Services.Directorio;
using Ybridio.Application.Services.Venta;
using Ybridio.Domain.Ventas;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Diagnostic;

namespace Ybridio.WinUI.ViewModels.Ventas;

/// <summary>
/// ViewModel del documento de venta PYME.
/// Ciclo: Borrador → Confirmar (descuenta inventario, genera CxC si Crédito) → RegistrarPago.
/// </summary>
/// <remarks>
/// Fórmulas runtime (docs/VENTAS_OPERATIVAS.md):
/// - Importe línea  = Cantidad × PrecioUnitario  (persistido al guardar)
/// - Total          = SUM(Detalles.Importe)       (persistido al confirmar)
/// - SaldoPendiente = Total - TotalPagado         (runtime, NO almacenado)
/// </remarks>
public sealed partial class VentaDocumentoViewModel : ObservableObject
{
    private readonly IVentaDocumentalService          _service;
    private readonly IRelacionComercialService         _relacionComercialService;
    private readonly IErpAuthorizationService         _auth;
    private readonly SessionService                   _session;
    private readonly ICurrentContextTracker           _contextTracker;

    private VentaDocumentalDto? _documento;
    // Entidad de Directorio seleccionada (ADR-038). RelacionComercialId se resuelve en GuardarAsync.
    private DirectorioSelectorDto? _entidadDirectorioSeleccionada;

    // ── Estado de UI ───────────────────────────────────────────────────────────
    [ObservableProperty] private bool         _isNuevo        = true;
    [ObservableProperty] private bool         _isBusy;
    [ObservableProperty] private string       _errorMessage   = string.Empty;
    [ObservableProperty] private string       _successMessage = string.Empty;

    // ── Campos de encabezado ───────────────────────────────────────────────────
    [ObservableProperty] private string       _nombreCliente  = string.Empty;
    [ObservableProperty] private int?         _relacionComercialId;
    [ObservableProperty] private TipoPago     _tipoPagoVenta  = TipoPago.Contado;
    [ObservableProperty] private DateTime     _fecha          = DateTime.Today;
    [ObservableProperty] private string?      _observaciones;
    [ObservableProperty] private EstatusVenta _estatusVenta   = EstatusVenta.Borrador;
    [ObservableProperty] private string?      _folio;

    // ── Totales ────────────────────────────────────────────────────────────────
    [ObservableProperty] private decimal _total;
    [ObservableProperty] private decimal _totalPagado;

    /// <summary>SaldoPendiente = Total - TotalPagado. Calculado runtime, NO persistido.</summary>
    public decimal SaldoPendiente => Total - TotalPagado;

    /// <summary>Wrapper DateTimeOffset para el DatePicker WinUI.</summary>
    public DateTimeOffset FechaOffset
    {
        get => new DateTimeOffset(Fecha);
        set { Fecha = value.DateTime; OnPropertyChanged(nameof(FechaOffset)); }
    }

    // ── Colecciones ────────────────────────────────────────────────────────────
    public ObservableCollection<DetalleLineaEditable> Detalles { get; } = [];
    public ObservableCollection<PagoVentaDto>         Pagos    { get; } = [];

    // ── Propiedades derivadas ──────────────────────────────────────────────────
    /// <summary>Título del documento: usa el folio cuando existe, o el ID como fallback.</summary>
    public string TituloDocumento => IsNuevo
        ? "Nueva Venta"
        : !string.IsNullOrEmpty(Folio) ? $"Venta {Folio}" : $"Venta #{_documento?.Id}";

    public string EstatusTexto => EstatusVenta switch
    {
        EstatusVenta.Borrador      => "Borrador",
        EstatusVenta.PendientePago => "Pendiente de Pago",
        EstatusVenta.Pagada        => "Pagada",
        EstatusVenta.Facturada     => "Facturada",
        EstatusVenta.Entregada     => "Entregada",
        EstatusVenta.Cerrada       => "Cerrada",
        EstatusVenta.Cancelada     => "Cancelada",
        _                          => EstatusVenta.ToString()
    };

    public bool EsContado          => TipoPagoVenta == TipoPago.Contado;
    public bool EsCredito          => TipoPagoVenta == TipoPago.Credito;

    // ── Workflow guards ────────────────────────────────────────────────────────
    /// <summary>Solo editable en Borrador (antes de confirmación).</summary>
    public bool PuedeEditar        => EstatusVenta == EstatusVenta.Borrador;
    /// <summary>Se confirma cuando está en Borrador, tiene detalles, y ya fue guardada.</summary>
    public bool PuedeConfirmar     => EstatusVenta == EstatusVenta.Borrador && !IsNuevo && Detalles.Count > 0;
    /// <summary>Se puede registrar pago desde PendientePago o Pagada (abono parcial adicional).</summary>
    public bool PuedeRegistrarPago => EstatusVenta is EstatusVenta.PendientePago or EstatusVenta.Pagada && !IsNuevo;
    /// <summary>Se puede cerrar cuando saldo = 0 (Pagada o superior).</summary>
    public bool PuedeCerrar        => !IsNuevo && EstatusVenta is EstatusVenta.Pagada or EstatusVenta.Facturada or EstatusVenta.Entregada && SaldoPendiente <= 0;
    /// <summary>Se puede cancelar mientras no esté Cerrada ni ya Cancelada.</summary>
    public bool PuedeCancelar      => !IsNuevo && EstatusVenta is not (EstatusVenta.Cerrada or EstatusVenta.Cancelada);

    /// <summary>ID de empresa del contexto de sesión, expuesto para binding en el selector control.</summary>
    public int EmpresaId => _session.EmpresaId;

    /// <summary>
    /// AlmacenId para confirmar. 0 = resolver automáticamente el principal de la sucursal (recomendado).
    /// Solo sobreescribir si el usuario selecciona explícitamente un almacén diferente.
    /// </summary>
    public int AlmacenIdConfirmacion { get; set; } = 0;

    /// <summary>
    /// Entidad de Directorio actualmente seleccionada.
    /// Expuesta para que la Page pueda restaurar el chip del selector al cargar un documento existente.
    /// </summary>
    public DirectorioSelectorDto? EntidadDirectorioSeleccionada => _entidadDirectorioSeleccionada;

    /// <summary>
    /// Selecciona una entidad del Directorio desde el selector institucional (ADR-038).
    /// RelacionComercialId se resolverá mediante GetOrCreate al guardar.
    /// </summary>
    public void SeleccionarCliente(DirectorioSelectorDto? entidad)
    {
        _entidadDirectorioSeleccionada = entidad;
        NombreCliente = entidad?.DisplayName ?? string.Empty;
    }

    /// <summary>Limpia la selección de cliente. Llamado cuando el usuario borra el selector.</summary>
    public void LimpiarCliente()
    {
        _entidadDirectorioSeleccionada = null;
        RelacionComercialId = null;
        NombreCliente       = string.Empty;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EliminarDetalleCommand))]
    private DetalleLineaEditable? _detalleSeleccionado;

    // ── Constructor ────────────────────────────────────────────────────────────

    /// <summary>
    /// Inicializa el ViewModel con los servicios requeridos.
    /// </summary>
    /// <param name="service">Servicio de ventas documentales.</param>
    /// <param name="auth">Servicio de autorización runtime.</param>
    /// <param name="session">Sesión activa del usuario.</param>
    /// <param name="contextTracker">Rastreador de contexto de navegación.</param>
    public VentaDocumentoViewModel(
        IVentaDocumentalService  service,
        IRelacionComercialService relacionComercialService,
        IErpAuthorizationService auth,
        SessionService           session,
        ICurrentContextTracker   contextTracker)
    {
        _service                  = service;
        _relacionComercialService = relacionComercialService;
        _auth           = auth;
        _session        = session;
        _contextTracker = contextTracker;
        Detalles.CollectionChanged += (_, _) => RecalcularTotales();
        Pagos.CollectionChanged    += (_, _) =>
        {
            OnPropertyChanged(nameof(TotalAnticipos));
            OnPropertyChanged(nameof(TieneAnticipos));
            OnPropertyChanged(nameof(PagosAnticipos));
        };
    }

    // ── Inicialización ─────────────────────────────────────────────────────────

    /// <summary>Carga un documento existente o establece modo Nuevo.</summary>
    /// <param name="venta">DTO de venta, o null para nueva.</param>
    public void Initialize(VentaDocumentalDto? venta)
    {
        _documento = venta;
        IsNuevo    = venta is null;

        if (venta is not null)
        {
            NombreCliente       = venta.NombreCliente;
            RelacionComercialId = venta.RelacionComercialId;
            TipoPagoVenta  = venta.TipoPago;
            Fecha          = venta.Fecha;
            Observaciones  = venta.Observaciones;
            EstatusVenta   = venta.Estatus;
            Total          = venta.Total;
            TotalPagado    = venta.TotalPagado;
            Folio          = venta.Folio;

            // Selector DTO Hydration Rule (ADR): restaurar chip del selector al cargar documento existente.
            if (venta.RelacionComercialId.HasValue && !string.IsNullOrEmpty(venta.NombreCliente))
            {
                _entidadDirectorioSeleccionada = new DirectorioSelectorDto
                {
                    EntityType         = DirectorioEntityType.Empresa,
                    EmpresaComercialId = venta.RelacionComercialId,
                    DisplayName        = venta.NombreCliente,
                };
            }
            else if (!string.IsNullOrEmpty(venta.NombreCliente))
            {
                _entidadDirectorioSeleccionada = new DirectorioSelectorDto
                {
                    EntityType  = DirectorioEntityType.Persona,
                    DisplayName = venta.NombreCliente,
                };
            }

            Detalles.Clear();
            foreach (var d in venta.Detalles)
                Detalles.Add(new DetalleLineaEditable(d.Id, d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario));

            Pagos.Clear();
            foreach (var p in venta.Pagos)
                Pagos.Add(p);
        }

        RecalcularTotales();
        RefrescarPermisosUI();
        _contextTracker.SetModuleContext("Ventas", "Documento Venta");
    }

    // ── Comandos ───────────────────────────────────────────────────────────────

    /// <summary>Guarda la venta (crea o actualiza encabezado).</summary>
    [RelayCommand]
    public async Task GuardarAsync(CancellationToken ct = default)
    {
        if (_session.Usuario is null) { ErrorMessage = "Sesion no activa."; return; }
        if (string.IsNullOrWhiteSpace(NombreCliente)) { ErrorMessage = "El cliente es obligatorio."; return; }
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            // ADR-038: GetOrCreate RelacionComercial bajo demanda
            if (_entidadDirectorioSeleccionada is not null)
            {
                var rc = await _relacionComercialService.GetOrCreateAsync(
                    _session.EmpresaId, _entidadDirectorioSeleccionada, _session.Usuario.Id, ct);
                if (!rc.Success) { ErrorMessage = rc.Error ?? "No se pudo vincular el cliente."; return; }
                RelacionComercialId = rc.Value;
            }
            if (IsNuevo)
            {
                if (Detalles.Count == 0) { ErrorMessage = "Debe agregar al menos un producto."; return; }
                var dto = new CrearVentaDocumentalDto(
                    EmpresaId:     _session.EmpresaId,
                    SucursalId:    _session.SucursalId,
                    RelacionComercialId:     RelacionComercialId,
                    NombreCliente: NombreCliente.Trim(),
                    TipoPago:      TipoPagoVenta,
                    Fecha:         Fecha,
                    PedidoId:      null,
                    Observaciones: Observaciones?.Trim(),
                    Detalles:      Detalles.Select(d =>
                        new CrearDetalleLineaDto(d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario)).ToList());

                var r = await _service.CrearAsync(dto, _session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo crear."; return; }
                var idDisplay = !string.IsNullOrEmpty(r.Value!.Folio) ? r.Value.Folio : $"#{r.Value.Id}";
                SuccessMessage = $"Venta {idDisplay} creada.";
                Initialize(r.Value);
            }
            else
            {
                var dto = new ActualizarVentaDocumentalDto(RelacionComercialId, NombreCliente.Trim(), TipoPagoVenta, Fecha, Observaciones?.Trim());
                var r   = await _service.ActualizarAsync(_documento!.Id, dto, _session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo actualizar."; return; }
                SuccessMessage = "Venta actualizada.";
                _documento = r.Value;
            }
        }
        finally { IsBusy = false; }
    }

    private bool PuedeConfirmarGuard() => PuedeConfirmar;

    /// <summary>Confirma la venta: descuenta inventario y genera CxC si TipoPago=Crédito.</summary>
    [RelayCommand(CanExecute = nameof(PuedeConfirmarGuard))]
    public async Task ConfirmarAsync(CancellationToken ct = default)
    {
        if (_documento is null || _session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.ConfirmarAsync(_documento.Id, AlmacenIdConfirmacion, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo confirmar."; return; }
            SuccessMessage = "Venta confirmada. Inventario descontado.";
            Initialize(r.Value);
        }
        finally { IsBusy = false; }
    }

    /// <summary>Agrega una línea de detalle. Si la venta ya existe, persiste inmediatamente.</summary>
    /// <param name="detalle">Línea editable a agregar.</param>
    /// <returns>True si se agregó correctamente.</returns>
    public async Task<bool> AgregarDetalleLocalAsync(DetalleLineaEditable detalle)
    {
        if (IsNuevo) { Detalles.Add(detalle); return true; }
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

    private bool HayDetalleGuard() => DetalleSeleccionado is not null;

    /// <summary>Elimina el detalle seleccionado.</summary>
    [RelayCommand(CanExecute = nameof(HayDetalleGuard))]
    public async Task EliminarDetalleAsync(CancellationToken ct = default)
    {
        if (DetalleSeleccionado is null) return;
        if (IsNuevo) { Detalles.Remove(DetalleSeleccionado); DetalleSeleccionado = null; return; }
        if (_session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.EliminarDetalleAsync(DetalleSeleccionado.Id, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo eliminar."; return; }
            Detalles.Remove(DetalleSeleccionado); DetalleSeleccionado = null; RecalcularTotales();
        }
        finally { IsBusy = false; }
    }

    /// <summary>Registra un pago parcial o total contra la venta.</summary>
    /// <param name="monto">Monto del pago.</param>
    /// <param name="formaPago">Forma de pago (Efectivo, Transferencia, etc.).</param>
    /// <param name="referencia">Referencia opcional.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>True si el pago se registró correctamente.</returns>
    public async Task<bool> RegistrarPagoAsync(decimal monto, string formaPago, string? referencia, CancellationToken ct = default)
    {
        if (_documento is null || _session.Usuario is null) return false;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.RegistrarPagoAsync(
                new RegistrarPagoVentaDto(_documento.Id, monto, formaPago, referencia),
                _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo registrar el pago."; return false; }
            Pagos.Add(r.Value!);
            TotalPagado += monto;
            OnPropertyChanged(nameof(SaldoPendiente));
            SuccessMessage = $"Pago de {monto:C2} registrado.";
            RefrescarPermisosUI();
            return true;
        }
        finally { IsBusy = false; }
    }

    /// <summary>Cancela la venta activa.</summary>
    public async Task<bool> CancelarVentaAsync(CancellationToken ct = default)
    {
        if (_documento is null || _session.Usuario is null) return false;
        var r = await _service.CancelarAsync(_documento.Id, _session.Usuario.Id, ct);
        if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo cancelar."; return false; }
        EstatusVenta   = EstatusVenta.Cancelada;
        SuccessMessage = "Venta cancelada."; RefrescarPermisosUI();
        return true;
    }

    /// <summary>Cierra formalmente la venta cuando saldo = 0.</summary>
    public async Task<bool> CerrarVentaAsync(CancellationToken ct = default)
    {
        if (_documento is null || _session.Usuario is null) return false;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.CerrarAsync(_documento.Id, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo cerrar la venta."; return false; }
            EstatusVenta   = EstatusVenta.Cerrada;
            SuccessMessage = "Venta cerrada."; RefrescarPermisosUI();
            return true;
        }
        finally { IsBusy = false; }
    }

    /// <summary>ID del documento guardado. Null si es nuevo.</summary>
    public long? DocumentoId => _documento?.Id;

    /// <summary>ID del Pedido origen, si esta venta fue generada desde un pedido. Null si es venta directa.</summary>
    public long? PedidoOrigenId => _documento?.PedidoId;

    /// <summary>True cuando la venta tiene un pedido origen navegable.</summary>
    public bool TienePedidoOrigen => _documento?.PedidoId.HasValue == true;

    /// <summary>Folio del pedido origen formateado como "PED-{id}". Null si no hay pedido origen.</summary>
    public string? PedidoOrigenFolio => _documento?.PedidoId is {} id ? $"PED-{id}" : null;

    /// <summary>Muestra el bloque operaciones cuando la venta está confirmada o en estado posterior.</summary>
    public bool MostrarOperacionesRealizadas =>
        !IsNuevo && EstatusVenta is not EstatusVenta.Borrador;

    /// <summary>Texto resumen de la operación principal de la venta.</summary>
    public string ResumenOperacion => EstatusVenta switch
    {
        EstatusVenta.PendientePago => "Venta confirmada. Inventario descontado.",
        EstatusVenta.Pagada        => "Venta pagada y confirmada.",
        EstatusVenta.Cerrada       => "Venta cerrada. Saldo liquidado.",
        EstatusVenta.Cancelada     => "Venta cancelada.",
        _                          => string.Empty
    };

    /// <summary>Ventas documentales no implementan descuentos por línea. Siempre falso.</summary>
    public bool TieneDescuentoGlobal => false;

    /// <summary>Ventas documentales no implementan descuentos por línea. Siempre cero.</summary>
    public decimal DescuentoTotalCalculado => 0m;

    /// <summary>Suma de pagos catalogados como anticipos (FormaPago contiene "Anticipo").</summary>
    public decimal TotalAnticipos => Pagos
        .Where(p => p.FormaPago.Contains("Anticipo", StringComparison.OrdinalIgnoreCase))
        .Sum(p => p.Monto);

    /// <summary>True cuando hay al menos un pago categorizado como anticipo.</summary>
    public bool TieneAnticipos => TotalAnticipos > 0;

    /// <summary>Lista filtrada de pagos catalogados como anticipos. Usada por Bloque F del Document Surface.</summary>
    public IReadOnlyList<PagoVentaDto> PagosAnticipos =>
        Pagos.Where(p => p.FormaPago.Contains("Anticipo", StringComparison.OrdinalIgnoreCase))
             .ToList();

    /// <summary>Número de líneas de detalle en el documento.</summary>
    public int TotalLineas => Detalles.Count;

    /// <summary>Suma de cantidades de todas las líneas de detalle.</summary>
    public int TotalArticulos => (int)Detalles.Sum(d => d.Cantidad);

    // ── Inicialización avanzada ────────────────────────────────────────────────

    /// <summary>
    /// Carga una venta existente por Id directamente desde el servicio.
    /// Usado cuando el Document Surface se abre desde el grid (ADR-032).
    /// </summary>
    /// <param name="ventaId">Id de la venta a cargar.</param>
    public async Task CargarVentaExistenteAsync(long ventaId, CancellationToken ct = default)
    {
        IsBusy = true; ErrorMessage = string.Empty;
        try
        {
            var r = await _service.ObtenerConDetallesAsync(ventaId, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo cargar la venta."; return; }
            Initialize(r.Value);
        }
        finally { IsBusy = false; }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void RecalcularTotales()
    {
        Total = Detalles.Sum(d => d.Importe);
        OnPropertyChanged(nameof(SaldoPendiente));
        RefrescarPermisosUI();
    }

    private void RefrescarPermisosUI()
    {
        OnPropertyChanged(nameof(PuedeEditar));
        OnPropertyChanged(nameof(PuedeConfirmar));
        OnPropertyChanged(nameof(PuedeRegistrarPago));
        OnPropertyChanged(nameof(PuedeCancelar));
        OnPropertyChanged(nameof(PuedeCerrar));
        OnPropertyChanged(nameof(EstatusTexto));
        OnPropertyChanged(nameof(EsContado));
        OnPropertyChanged(nameof(EsCredito));
        OnPropertyChanged(nameof(TituloDocumento));
        OnPropertyChanged(nameof(SaldoPendiente));
        ConfirmarCommand.NotifyCanExecuteChanged();
        EliminarDetalleCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(TienePedidoOrigen));
        OnPropertyChanged(nameof(PedidoOrigenFolio));
        OnPropertyChanged(nameof(MostrarOperacionesRealizadas));
        OnPropertyChanged(nameof(ResumenOperacion));
        OnPropertyChanged(nameof(TieneDescuentoGlobal));
        OnPropertyChanged(nameof(DescuentoTotalCalculado));
        OnPropertyChanged(nameof(TotalAnticipos));
        OnPropertyChanged(nameof(TieneAnticipos));
        OnPropertyChanged(nameof(PagosAnticipos));
        OnPropertyChanged(nameof(TotalLineas));
        OnPropertyChanged(nameof(TotalArticulos));
    }
}

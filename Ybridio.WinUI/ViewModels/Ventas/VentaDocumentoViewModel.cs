using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
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
    private readonly IErpAuthorizationService         _auth;
    private readonly SessionService                   _session;
    private readonly ICurrentContextTracker           _contextTracker;

    private VentaDocumentalDto? _documento;

    // ── Estado de UI ───────────────────────────────────────────────────────────
    [ObservableProperty] private bool         _isNuevo        = true;
    [ObservableProperty] private bool         _isBusy;
    [ObservableProperty] private string       _errorMessage   = string.Empty;
    [ObservableProperty] private string       _successMessage = string.Empty;

    // ── Campos de encabezado ───────────────────────────────────────────────────
    [ObservableProperty] private string       _nombreCliente  = string.Empty;
    [ObservableProperty] private int?         _clienteId;
    [ObservableProperty] private TipoPago     _tipoPagoVenta  = TipoPago.Contado;
    [ObservableProperty] private DateTime     _fecha          = DateTime.Today;
    [ObservableProperty] private string?      _observaciones;
    [ObservableProperty] private EstatusVenta _estatusVenta   = EstatusVenta.Borrador;

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
    public string TituloDocumento   => IsNuevo ? "Nueva Venta" : $"Venta #{_documento?.Id}";

    public string EstatusTexto => EstatusVenta switch
    {
        EstatusVenta.Borrador   => "Borrador",
        EstatusVenta.Confirmada => "Confirmada",
        EstatusVenta.Cancelada  => "Cancelada",
        _                       => EstatusVenta.ToString()
    };

    public bool EsContado          => TipoPagoVenta == TipoPago.Contado;
    public bool EsCredito          => TipoPagoVenta == TipoPago.Credito;
    public bool PuedeEditar        => EstatusVenta == EstatusVenta.Borrador;
    public bool PuedeConfirmar     => EstatusVenta == EstatusVenta.Borrador && !IsNuevo && Detalles.Count > 0;
    public bool PuedeRegistrarPago => EstatusVenta == EstatusVenta.Confirmada && !IsNuevo;
    public bool PuedeCancelar      => EstatusVenta == EstatusVenta.Borrador && !IsNuevo;

    /// <summary>Almacén por defecto (PYME: 1). La Page puede sobreescribir antes de confirmar.</summary>
    public int AlmacenIdConfirmacion { get; set; } = 1;

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
        IErpAuthorizationService auth,
        SessionService           session,
        ICurrentContextTracker   contextTracker)
    {
        _service        = service;
        _auth           = auth;
        _session        = session;
        _contextTracker = contextTracker;
        Detalles.CollectionChanged += (_, _) => RecalcularTotales();
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
            NombreCliente  = venta.NombreCliente;
            ClienteId      = venta.ClienteId;
            TipoPagoVenta  = venta.TipoPago;
            Fecha          = venta.Fecha;
            Observaciones  = venta.Observaciones;
            EstatusVenta   = venta.Estatus;
            Total          = venta.Total;
            TotalPagado    = venta.TotalPagado;

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
            if (IsNuevo)
            {
                if (Detalles.Count == 0) { ErrorMessage = "Debe agregar al menos un producto."; return; }
                var dto = new CrearVentaDocumentalDto(
                    EmpresaId:     _session.EmpresaId,
                    SucursalId:    _session.SucursalId,
                    ClienteId:     ClienteId,
                    NombreCliente: NombreCliente.Trim(),
                    TipoPago:      TipoPagoVenta,
                    Fecha:         Fecha,
                    PedidoId:      null,
                    Observaciones: Observaciones?.Trim(),
                    Detalles:      Detalles.Select(d =>
                        new CrearDetalleLineaDto(d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario)).ToList());

                var r = await _service.CrearAsync(dto, _session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo crear."; return; }
                SuccessMessage = $"Venta #{r.Value!.Id} creada.";
                Initialize(r.Value);
            }
            else
            {
                var dto = new ActualizarVentaDocumentalDto(ClienteId, NombreCliente.Trim(), TipoPagoVenta, Fecha, Observaciones?.Trim());
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
    /// <returns>True si se canceló correctamente.</returns>
    public async Task<bool> CancelarVentaAsync(CancellationToken ct = default)
    {
        if (_documento is null || _session.Usuario is null) return false;
        var r = await _service.CancelarAsync(_documento.Id, _session.Usuario.Id, ct);
        if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo cancelar."; return false; }
        EstatusVenta  = EstatusVenta.Cancelada;
        SuccessMessage = "Venta cancelada."; RefrescarPermisosUI();
        return true;
    }

    /// <summary>ID del documento guardado. Null si es nuevo.</summary>
    public long? DocumentoId => _documento?.Id;

    /// <summary>ID del Pedido origen, si esta venta fue generada desde un pedido. Null si es venta directa.</summary>
    public long? PedidoOrigenId => _documento?.PedidoId;

    /// <summary>True cuando la venta tiene un pedido origen navegable.</summary>
    public bool TienePedidoOrigen => _documento?.PedidoId.HasValue == true;

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
        OnPropertyChanged(nameof(EstatusTexto));
        OnPropertyChanged(nameof(EsContado));
        OnPropertyChanged(nameof(EsCredito));
        OnPropertyChanged(nameof(TituloDocumento));
        ConfirmarCommand.NotifyCanExecuteChanged();
        EliminarDetalleCommand.NotifyCanExecuteChanged();
    }
}

using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Ventas;

/// <summary>
/// Compromiso operacional del cliente con la empresa.
/// Un pedido representa la intención confirmada de compra o servicio.
/// Puede originarse desde una cotización aprobada o crearse directamente.
/// <para>
/// Flujo: Nuevo → Confirmado → EnProceso → Completado | Cancelado.
/// Un pedido Completado puede originar una Venta (entrega + cobro) o una OrdenTrabajo.
/// </para>
/// </summary>
public class Pedido : AuditableEntity
{
    public long  Id          { get; set; }
    public int   EmpresaId   { get; set; }
    public int?  SucursalId  { get; set; }

    /// <summary>Relación comercial vinculada al pedido (nullable para mostrador).</summary>
    public int?   RelacionComercialId { get; set; }
    /// <summary>Nombre del cliente al momento del pedido (denormalizado).</summary>
    public string NombreCliente { get; set; } = string.Empty;

    /// <summary>Cotización de origen, si el pedido fue generado desde una cotización aprobada.</summary>
    public long? CotizacionId  { get; set; }

    /// <summary>Folio documental (ej: "PED-000001"). Null en registros anteriores a SerieDocumento.</summary>
    public string? Folio { get; set; }

    public EstatusPedido Estatus              { get; set; } = EstatusPedido.Borrador;
    public DateTime      Fecha                { get; set; }
    public DateTime?     FechaEntregaCompromiso { get; set; }

    /// <summary>
    /// Subtotal = SUM(detalle.Importe neto). Null en registros anteriores a ADR-WorkflowV1.
    /// Reservado para distinción subtotal/total cuando se agreguen cargos al pedido.
    /// </summary>
    public decimal? Subtotal     { get; set; }

    /// <summary>
    /// Total del pedido.
    /// Fórmula: SUM(detalle.Importe) + SUM(cargos). Persistido para acceso rápido en reportes.
    /// </summary>
    public decimal Total         { get; set; }
    public string? Observaciones  { get; set; }

    // ── Dimensión financiera (independiente del workflow operacional) ──────────

    /// <summary>
    /// Monto mínimo de anticipo requerido para iniciar operación (ej: generar OT).
    /// Null = sin anticipo requerido. Configurable por pedido.
    /// </summary>
    public decimal? AnticipoRequerido { get; set; }

    /// <summary>
    /// Suma acumulada de todos los anticipos registrados contra este pedido.
    /// Actualizado automáticamente al registrar/cancelar un <see cref="AnticipoPedido"/>.
    /// </summary>
    public decimal AnticipoPagado { get; set; }

    /// <summary>
    /// Estado financiero del pedido — dimensión independiente de <see cref="EstatusPedido"/>.
    /// Calculado automáticamente en cada operación de pago.
    /// </summary>
    public EstadoFinancieroPedido EstadoFinanciero { get; set; } = EstadoFinancieroPedido.SinPago;

    // Navegación
    public Empresa              Empresa             { get; set; } = null!;
    public Sucursal?            Sucursal            { get; set; }
    public RelacionComercial?   RelacionComercial   { get; set; }
    public Cotizacion?          Cotizacion          { get; set; }
    public ICollection<PedidoDetalle>  Detalles     { get; set; } = [];
    /// <summary>Cargos accesorios del pedido (Flete, Maniobras, Seguro, etc.) — Commercial Charges Pattern.</summary>
    public ICollection<PedidoCargo>    Cargos       { get; set; } = [];
    /// <summary>Anticipos y pagos parciales registrados contra este pedido.</summary>
    public ICollection<AnticipoPedido> Anticipos    { get; set; } = [];
}

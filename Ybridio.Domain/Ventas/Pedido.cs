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

    public int?   ClienteId     { get; set; }
    /// <summary>Nombre del cliente al momento del pedido (denormalizado).</summary>
    public string NombreCliente { get; set; } = string.Empty;

    /// <summary>Cotización de origen, si el pedido fue generado desde una cotización aprobada.</summary>
    public long? CotizacionId  { get; set; }

    public EstatusPedido Estatus              { get; set; } = EstatusPedido.Nuevo;
    public DateTime      Fecha                { get; set; }
    public DateTime?     FechaEntregaCompromiso { get; set; }

    /// <summary>
    /// Total del pedido.
    /// Fórmula: SUM(detalle.Importe). Persistido para acceso rápido en reportes.
    /// </summary>
    public decimal Total         { get; set; }
    public string? Observaciones  { get; set; }

    // Navegación
    public Empresa     Empresa     { get; set; } = null!;
    public Sucursal?   Sucursal    { get; set; }
    public Cliente?    Cliente     { get; set; }
    public Cotizacion? Cotizacion  { get; set; }
    public ICollection<PedidoDetalle> Detalles { get; set; } = [];
}

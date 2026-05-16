using Ybridio.Domain.Common;

namespace Ybridio.Domain.Ventas;

/// <summary>
/// Registro de anticipo o pago parcial contra un Pedido.
/// Análogo a <see cref="PagoVenta"/> pero perteneciente al ciclo operacional del Pedido,
/// antes de generar la Venta final.
/// <para>
/// Cada anticipo incrementa <see cref="Pedido.AnticipoPagado"/> y recalcula
/// <see cref="Pedido.EstadoFinanciero"/>.
/// </para>
/// </summary>
public class AnticipoPedido : AuditableEntity
{
    public long Id { get; set; }

    /// <summary>FK al Pedido al que pertenece este anticipo.</summary>
    public long PedidoId { get; set; }

    /// <summary>Fecha en que se recibió el pago.</summary>
    public DateTime Fecha { get; set; }

    /// <summary>Monto recibido en este anticipo.</summary>
    public decimal Monto { get; set; }

    /// <summary>Forma de pago: "Efectivo", "Transferencia", "Tarjeta", etc.</summary>
    public string FormaPago { get; set; } = "Efectivo";

    /// <summary>Número de referencia, comprobante o nota del anticipo.</summary>
    public string? Referencia { get; set; }

    // Navegación
    public Pedido Pedido { get; set; } = null!;
}

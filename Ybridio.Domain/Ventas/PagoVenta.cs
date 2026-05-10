using Ybridio.Domain.Common;

namespace Ybridio.Domain.Ventas;

/// <summary>
/// Pago parcial o total registrado contra una Venta.
/// Fórmula: Venta.SaldoPendiente = Venta.Total - SUM(PagoVenta.Monto) — calculado runtime.
/// </summary>
public class PagoVenta : AuditableEntity
{
    public long Id { get; set; }
    public long VentaId { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Monto { get; set; }
    /// <summary>Efectivo, Transferencia, Tarjeta, etc. Texto libre PYME.</summary>
    public string FormaPago { get; set; } = "Efectivo";
    public string? Referencia { get; set; }

    // Navegación
    public Venta Venta { get; set; } = null!;
}

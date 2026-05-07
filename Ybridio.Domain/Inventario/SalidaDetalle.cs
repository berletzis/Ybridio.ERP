using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;

namespace Ybridio.Domain.Inventario;

/// <summary>Línea de detalle de una salida de almacén.</summary>
public class SalidaDetalle : AuditableEntity
{
    public long Id { get; set; }
    public long SalidaId { get; set; }
    public int ProductoId { get; set; }
    public short NumeroLinea { get; set; }
    public decimal Cantidad { get; set; }
    public int? CantidadCajas { get; set; }
    public int? PiezasPorCaja { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal Importe { get; set; }
    public decimal? PrecioUnitario { get; set; }
    public decimal? Descuento { get; set; }
    public long? EntradaDetalleOrigenId { get; set; }
    public string? CodigoBarras { get; set; }
    public string? Sku { get; set; }
    public string? Observaciones { get; set; }

    // Navegación
    public Salida Salida { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
    public EntradaDetalle? EntradaDetalleOrigen { get; set; }
}

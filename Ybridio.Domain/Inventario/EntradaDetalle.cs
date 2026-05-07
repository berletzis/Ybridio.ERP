using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;

namespace Ybridio.Domain.Inventario;

/// <summary>Línea de detalle de una entrada de almacén.</summary>
public class EntradaDetalle : AuditableEntity
{
    public long Id { get; set; }
    public long EntradaId { get; set; }
    public int ProductoId { get; set; }
    public short NumeroLinea { get; set; }
    public decimal CantidadEsperada { get; set; }
    public decimal CantidadRecibida { get; set; }
    public int? CantidadCajas { get; set; }
    public int? PiezasPorCaja { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal Importe { get; set; }
    public string? CodigoBarras { get; set; }
    public string? Sku { get; set; }
    public string? Observaciones { get; set; }

    // Navegación
    public Entrada Entrada { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}

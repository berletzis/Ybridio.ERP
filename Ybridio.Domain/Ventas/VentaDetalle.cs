using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;
using Ybridio.Domain.Inventario;

namespace Ybridio.Domain.Ventas;

public class VentaDetalle : AuditableEntity
{
    public long Id { get; set; }
    public long VentaId { get; set; }
    public int ProductoId { get; set; }
    public decimal Cantidad { get; set; }
    public decimal? Precio { get; set; }
    public decimal? Importe { get; set; }
    public int? AlmacenId { get; set; }

    // Navegación
    public Venta Venta { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
    public Almacen? Almacen { get; set; }
}

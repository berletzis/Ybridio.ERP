using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;

namespace Ybridio.Domain.Compras;

public class OrdenCompraDetalle : AuditableEntity
{
    public long Id { get; set; }
    public long OrdenCompraId { get; set; }
    public int ProductoId { get; set; }
    public decimal Cantidad { get; set; }
    public decimal Precio { get; set; }
    public decimal Importe { get; set; }

    // Navegación
    public OrdenCompra OrdenCompra { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}

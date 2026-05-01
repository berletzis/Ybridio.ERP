using Ybridio.Domain.Catalogos;

namespace Ybridio.Domain.Compras;

/// <summary>
/// Detalle de recepción — no tiene auditoría completa en el script original.
/// </summary>
public class RecepcionCompraDetalle
{
    public long Id { get; set; }
    public long RecepcionCompraId { get; set; }
    public int ProductoId { get; set; }
    public decimal Cantidad { get; set; }

    // Navegación
    public RecepcionCompra RecepcionCompra { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}

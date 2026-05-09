using Ybridio.Domain.Catalogos;

namespace Ybridio.Domain.Ventas;

/// <summary>
/// Línea de detalle de un pedido.
/// Soporta productos del catálogo y servicios/ítems ad-hoc sin registro de producto.
/// </summary>
/// <remarks>
/// Fórmula Importe: Cantidad × PrecioUnitario. Persistido para trazabilidad histórica.
/// </remarks>
public class PedidoDetalle
{
    public long    Id             { get; set; }
    public long    PedidoId       { get; set; }
    public int?    ProductoId     { get; set; }
    public string  Descripcion    { get; set; } = string.Empty;
    public decimal Cantidad       { get; set; }
    public decimal PrecioUnitario { get; set; }
    /// <summary>Importe = Cantidad × PrecioUnitario. Persistido.</summary>
    public decimal Importe        { get; set; }

    // Navegación
    public Pedido    Pedido   { get; set; } = null!;
    public Producto? Producto { get; set; }
}

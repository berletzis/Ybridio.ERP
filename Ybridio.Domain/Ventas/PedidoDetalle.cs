using Ybridio.Domain.Catalogos;

namespace Ybridio.Domain.Ventas;

/// <summary>
/// Línea de detalle de un pedido.
/// Soporta productos del catálogo y servicios/ítems ad-hoc sin registro de producto.
/// </summary>
/// <remarks>
/// Fórmula Importe: Cantidad × PrecioUnitario × (1 − DescuentoPct/100). Persistido para trazabilidad histórica.
/// En conversiones COT→PED, el Importe ya viene neto (snapshot del precio acordado).
/// </remarks>
public class PedidoDetalle
{
    public long    Id             { get; set; }
    public long    PedidoId       { get; set; }
    public int?    ProductoId     { get; set; }
    public string  Descripcion    { get; set; } = string.Empty;
    public decimal Cantidad       { get; set; }
    public decimal PrecioUnitario { get; set; }
    /// <summary>Descuento porcentual por línea (0-100). Preservado desde cotización origen si aplica.</summary>
    public decimal DescuentoPct   { get; set; }
    /// <summary>Indica si esta línea aplica IVA. Preservado desde cotización origen si aplica.</summary>
    public bool    IvaAplicable   { get; set; } = true;
    /// <summary>Importe neto = Cantidad × PrecioUnitario × (1 − DescuentoPct/100). Persistido.</summary>
    public decimal Importe        { get; set; }

    // Navegación
    public Pedido    Pedido   { get; set; } = null!;
    public Producto? Producto { get; set; }
}

using Ybridio.Domain.Catalogos;

namespace Ybridio.Domain.Ventas;

/// <summary>
/// Línea de detalle de una cotización.
/// Puede referenciar un producto del catálogo (ProductoId) o ser un ítem ad-hoc
/// de servicio sin producto registrado (solo Descripcion).
/// </summary>
/// <remarks>
/// Fórmula Importe: Cantidad × PrecioUnitario
/// Calculado y almacenado en BD para integridad histórica (el precio puede cambiar).
/// </remarks>
public class CotizacionDetalle
{
    public long    Id             { get; set; }
    public long    CotizacionId   { get; set; }

    /// <summary>Producto del catálogo. Null si el ítem es un servicio o descripción libre.</summary>
    public int?    ProductoId     { get; set; }

    /// <summary>Descripción del ítem. Si ProductoId está definido, se llena desde el catálogo al crear.</summary>
    public string  Descripcion    { get; set; } = string.Empty;

    public decimal Cantidad       { get; set; }
    public decimal PrecioUnitario { get; set; }

    /// <summary>Importe = Cantidad × PrecioUnitario. Persistido para integridad histórica.</summary>
    public decimal Importe        { get; set; }

    // Navegación
    public Cotizacion Cotizacion { get; set; } = null!;
    public Producto?  Producto   { get; set; }
}

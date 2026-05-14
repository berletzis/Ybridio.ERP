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

    /// <summary>
    /// Porcentaje de descuento aplicado a esta línea (0–100).
    /// El importe neto ya refleja el descuento. Default 0 = sin descuento.
    /// </summary>
    /// <remarks>ADR-042 — Commercial Discount Pattern.</remarks>
    public decimal DescuentoPct   { get; set; }

    /// <summary>
    /// Importe neto = Cantidad × PrecioUnitario × (1 − DescuentoPct / 100).
    /// Persistido para integridad histórica del precio acordado.
    /// </summary>
    public decimal Importe        { get; set; }

    /// <summary>
    /// Indica si esta línea aplica IVA. Heredado del Producto al crear la línea.
    /// Se persiste para que el cálculo de impuestos sea correcto al recargar el documento.
    /// </summary>
    public bool    IvaAplicable   { get; set; } = true;

    // Navegación
    public Cotizacion Cotizacion { get; set; } = null!;
    public Producto?  Producto   { get; set; }
}

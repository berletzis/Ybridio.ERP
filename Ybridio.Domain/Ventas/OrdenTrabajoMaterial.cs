using Ybridio.Domain.Catalogos;

namespace Ybridio.Domain.Ventas;

/// <summary>
/// Material o servicio utilizado en una orden de trabajo.
/// Puede referenciar un producto del catálogo (con descuento de inventario) o ser
/// un ítem ad-hoc (mano de obra, servicio externo, etc.) sin FK a Producto.
/// </summary>
/// <remarks>
/// Fórmula Importe: Cantidad × PrecioUnitario. Persistido.
///
/// El descuento de inventario es opcional (DescontarInventario = true solo si el
/// material viene del almacén y debe afectar existencias). En V1 este campo
/// es informativo; la integración con MovimientoInventario se implementará en V1.1.
/// </remarks>
public class OrdenTrabajoMaterial
{
    public long    Id               { get; set; }
    public long    OrdenTrabajoId   { get; set; }

    /// <summary>Producto del catálogo. Null si es servicio u ítem ad-hoc.</summary>
    public int?    ProductoId       { get; set; }

    public string  Descripcion      { get; set; } = string.Empty;
    public decimal Cantidad         { get; set; }
    public decimal PrecioUnitario   { get; set; }
    /// <summary>Importe = Cantidad × PrecioUnitario. Persistido.</summary>
    public decimal Importe          { get; set; }

    // Navegación
    public OrdenTrabajo OrdenTrabajo { get; set; } = null!;
    public Producto?    Producto     { get; set; }
}

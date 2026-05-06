using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

/// <summary>
/// Configuración de precio y disponibilidad de un producto en una sucursal específica.
/// Permite sobrescribir el precio base del catálogo por sucursal.
/// No extiende AuditableEntity: es una tabla de configuración sin audit trail.
/// </summary>
public sealed class ProductoSucursal
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public int SucursalId { get; set; }

    /// <summary>Precio específico para esta sucursal. Null = usa el precio base del producto.</summary>
    public decimal? PrecioOverride { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; }

    // Navigation
    public Producto Producto { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
}

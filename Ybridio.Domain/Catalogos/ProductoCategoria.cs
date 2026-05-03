namespace Ybridio.Domain.Catalogos;

/// <summary>
/// Tabla de unión muchos-a-muchos entre Producto y CategoriaProducto.
/// No hereda de AuditableEntity: la tabla no tiene columna Borrado.
/// </summary>
public class ProductoCategoria
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public int CategoriaId { get; set; }
    public bool EsPrincipal { get; set; }
    public DateTime FechaCreacion { get; set; }

    // Navegación
    public Producto Producto { get; set; } = null!;
    public CategoriaProducto Categoria { get; set; } = null!;
}

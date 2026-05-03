// ── Ybridio.Domain/Catalogos/CategoriaProducto.cs ────────────────────────────
using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

public class CategoriaProducto : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;

    // Jerarquía (self-referencing)
    public int? CategoriaPadreId { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public CategoriaProducto? CategoriaPadre { get; set; }
    public ICollection<CategoriaProducto> SubCategorias { get; set; } = [];
    public ICollection<ProductoCategoria> ProductoCategorias { get; set; } = [];
}

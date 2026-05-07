using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Inventario;

/// <summary>
/// Almacén físico o lógico dentro de una Sucursal.
/// Jerarquía: Empresa → Sucursal → Almacén.
/// Una sucursal puede tener múltiples almacenes (Principal, Backstore, Dañados, Tránsito, etc.).
/// </summary>
public class Almacen : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int SucursalId { get; set; }
    /// <summary>Código corto único por sucursal (e.g. PRINCIPAL, BACK, DAÑO).</summary>
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    /// <summary>Identifica el almacén principal de la sucursal. Solo uno puede ser principal por sucursal.</summary>
    public bool EsPrincipal { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<Existencia> Existencias { get; set; } = [];
}

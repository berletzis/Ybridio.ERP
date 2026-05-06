using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Inventario;

public class Almacen : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int SucursalId { get; set; }
    public string Nombre { get; set; } = string.Empty;

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<Existencia> Existencias { get; set; } = [];
}

using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Inventario;

public class Existencia : AuditableEntity
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public int AlmacenId { get; set; }
    public int ProductoId { get; set; }
    public decimal Cantidad { get; set; }
    public int? SucursalId { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public Almacen Almacen { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
    public Sucursal? Sucursal { get; set; }
}

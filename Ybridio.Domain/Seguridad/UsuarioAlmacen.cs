using Ybridio.Domain.Inventario;

namespace Ybridio.Domain.Seguridad;

/// <summary>
/// Scope de seguridad a nivel almacén: restringe el acceso de un usuario
/// a almacenes específicos dentro de las sucursales que ya tiene asignadas
/// vía <see cref="UsuarioSucursal"/>.
/// <c>UsuarioId</c> referencia a <c>ApplicationUser</c> (en Infrastructure);
/// la relación se configura vía Fluent API sin navegación directa al usuario.
/// </summary>
public class UsuarioAlmacen
{
    public int  Id         { get; set; }
    public Guid UsuarioId  { get; set; }
    public int  AlmacenId  { get; set; }

    // Navegación
    public Almacen Almacen { get; set; } = null!;
}

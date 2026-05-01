using Microsoft.AspNetCore.Identity;
using Ybridio.Domain.Seguridad;

namespace Ybridio.Infrastructure.Persistence.Identity;

/// <summary>
/// Rol del sistema — extiende IdentityRole&lt;Guid&gt; y se mapea a seguridad.Rol.
/// Vive en Infrastructure porque IdentityRole es una dependencia de la capa de persistencia.
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public DateTime FechaCreacion { get; set; }
    public bool Borrado { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navegación
    public ICollection<RolPermiso> RolPermisos { get; set; } = [];
}

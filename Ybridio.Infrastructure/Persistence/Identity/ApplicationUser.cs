using Microsoft.AspNetCore.Identity;
using Ybridio.Domain.Core;
using Ybridio.Domain.Seguridad;

namespace Ybridio.Infrastructure.Persistence.Identity;

/// <summary>
/// Usuario del sistema — extiende IdentityUser&lt;Guid&gt; y se mapea a seguridad.Usuario.
/// Vive en Infrastructure porque IdentityUser es una dependencia de la capa de persistencia.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public Guid UsuarioCreacionId { get; set; }
    public DateTime? FechaModificacion { get; set; }
    public Guid? UsuarioModificacionId { get; set; }
    public bool Borrado { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public ICollection<UsuarioSucursal> UsuarioSucursales { get; set; } = [];
    public ICollection<UsuarioPermiso> UsuarioPermisos { get; set; } = [];
}

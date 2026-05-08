using Ybridio.Domain.Common;

namespace Ybridio.Domain.Seguridad;

/// <summary>
/// Perfil de permisos reutilizable. Agrupación nombrada de permisos que puede asignarse
/// a usuarios directamente, facilitando administración masiva sin modificar roles.
/// Los perfiles son globales (no están acotados por empresa).
/// </summary>
public class Perfil : CreationAuditEntity
{
    public int Id { get; set; }

    /// <summary>Nombre descriptivo del perfil (e.g. "POS Básico", "Inventario Supervisor").</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Descripción opcional del alcance y uso del perfil.</summary>
    public string? Descripcion { get; set; }

    /// <summary>Indica si el perfil está activo y sus permisos deben aplicarse.</summary>
    public bool Activo { get; set; } = true;

    // Navegación
    public ICollection<PerfilPermiso>  PerfilPermisos  { get; set; } = [];
    public ICollection<UsuarioPerfil>  UsuarioPerfiles { get; set; } = [];
}

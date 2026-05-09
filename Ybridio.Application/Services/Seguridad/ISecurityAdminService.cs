using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Seguridad;

namespace Ybridio.Application.Services.Seguridad;

/// <summary>
/// Servicio de administración de seguridad enterprise.
/// Provee queries y operaciones de escritura para la UI administrativa de seguridad:
/// usuarios con detalle, roles con conteos, permisos del sistema, y gestión de scopes.
/// NO reemplaza el motor de autorización runtime (IErpAuthorizationService).
/// </summary>
public interface ISecurityAdminService
{
    // ── Usuarios ─────────────────────────────────────────────────────────────

    /// <summary>Lista usuarios de la empresa con sus roles y perfiles concatenados para visualización.</summary>
    Task<IReadOnlyList<UsuarioResumenDto>> ListarUsuariosConDetalleAsync(
        int empresaId, CancellationToken ct = default);

    // ── Roles ─────────────────────────────────────────────────────────────────

    /// <summary>Lista roles con conteo de permisos y usuarios asignados.</summary>
    Task<IReadOnlyList<RolAdminDto>> ListarRolesConDetalleAsync(CancellationToken ct = default);

    /// <summary>Obtiene los IDs de permisos asignados a un rol.</summary>
    Task<IReadOnlyList<int>> ObtenerPermisosDeRolAsync(Guid rolId, CancellationToken ct = default);

    /// <summary>Reemplaza los permisos asignados a un rol.</summary>
    Task<ServiceResult> AsignarPermisosARolAsync(
        Guid rolId, IReadOnlyList<int> permisoIds, CancellationToken ct = default);

    // ── Permisos ──────────────────────────────────────────────────────────────

    /// <summary>Lista todos los permisos del sistema con información de módulo. Solo lectura.</summary>
    Task<IReadOnlyList<PermisoAdminDto>> ListarPermisosAsync(CancellationToken ct = default);

    /// <summary>Obtiene los IDs de permisos asignados a un perfil.</summary>
    Task<IReadOnlyList<int>> ObtenerPermisosDePerfilAsync(int perfilId, CancellationToken ct = default);

    // ── Scopes ────────────────────────────────────────────────────────────────

    /// <summary>Lista los scopes de acceso de todos los usuarios de la empresa.</summary>
    Task<IReadOnlyList<ScopeUsuarioDto>> ListarScopesUsuariosAsync(
        int empresaId, CancellationToken ct = default);

    /// <summary>Lista todas las sucursales disponibles de la empresa (para el selector de scopes).</summary>
    Task<IReadOnlyList<SucursalScopeItem>> ListarSucursalesDisponiblesAsync(
        int empresaId, CancellationToken ct = default);

    /// <summary>Lista todos los almacenes disponibles de la empresa (para el selector de scopes).</summary>
    Task<IReadOnlyList<AlmacenScopeItem>> ListarAlmacenesDisponiblesAsync(
        int empresaId, CancellationToken ct = default);

    /// <summary>Obtiene los IDs de sucursales asignadas como scope a un usuario.</summary>
    Task<IReadOnlyList<int>> ObtenerSucursalesDeUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>Reemplaza las sucursales de scope de un usuario.</summary>
    Task<ServiceResult> AsignarSucursalesAUsuarioAsync(
        Guid usuarioId, IReadOnlyList<int> sucursalIds, CancellationToken ct = default);

    /// <summary>Obtiene los IDs de almacenes asignados como scope a un usuario.</summary>
    Task<IReadOnlyList<int>> ObtenerAlmacenesDeUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>Reemplaza los almacenes de scope de un usuario.</summary>
    Task<ServiceResult> AsignarAlmacenesAUsuarioAsync(
        Guid usuarioId, IReadOnlyList<int> almacenIds, CancellationToken ct = default);

    // ── Asignación de perfiles a usuario ─────────────────────────────────────

    /// <summary>Obtiene los IDs de perfiles asignados a un usuario.</summary>
    Task<IReadOnlyList<int>> ObtenerPerfilesDeUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>Reemplaza los perfiles asignados a un usuario.</summary>
    Task<ServiceResult> AsignarPerfilesAUsuarioAsync(
        Guid usuarioId, IReadOnlyList<int> perfilIds, CancellationToken ct = default);
}

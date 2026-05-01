using Ybridio.Application.Common;

namespace Ybridio.Application.Services.Permisos;

/// <summary>
/// Contrato para la evaluación de permisos de usuario con soporte a override por usuario
/// y herencia desde rol.
/// </summary>
public interface IPermisoService
{
    /// <summary>
    /// Determina si el usuario tiene el permiso indicado por su clave.
    /// Evaluación: UsuarioPermiso (override) → RolPermiso.
    /// null en UsuarioPermiso = hereda del rol; true/false = sobrescribe.
    /// </summary>
    Task<bool> TienePermisoAsync(Guid usuarioId, string clave, CancellationToken ct = default);

    /// <summary>
    /// Retorna el conjunto de claves de permisos efectivos para el usuario.
    /// </summary>
    Task<IReadOnlySet<string>> ObtenerPermisosEfectivosAsync(
        Guid usuarioId, CancellationToken ct = default);
}

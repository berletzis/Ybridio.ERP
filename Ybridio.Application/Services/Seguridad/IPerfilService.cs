using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Seguridad;

namespace Ybridio.Application.Services.Seguridad;

/// <summary>
/// Gestión de perfiles de permisos reutilizables.
/// Un perfil agrupa permisos que pueden asignarse en bloque a usuarios.
/// </summary>
public interface IPerfilService
{
    /// <summary>Retorna todos los perfiles activos.</summary>
    Task<IReadOnlyList<PerfilDto>> ListarAsync(CancellationToken ct = default);

    /// <summary>Retorna el perfil por su ID, o null si no existe.</summary>
    Task<PerfilDto?> ObtenerPorIdAsync(int perfilId, CancellationToken ct = default);

    /// <summary>Crea un nuevo perfil con los permisos indicados.</summary>
    Task<ServiceResult<PerfilDto>> CrearAsync(CrearPerfilDto dto, CancellationToken ct = default);

    /// <summary>Actualiza nombre, descripción y estado activo de un perfil.</summary>
    Task<ServiceResult<PerfilDto>> ActualizarAsync(int perfilId, ActualizarPerfilDto dto, CancellationToken ct = default);

    /// <summary>Reemplaza completamente los permisos asignados al perfil.</summary>
    Task<ServiceResult> AsignarPermisosAsync(int perfilId, IReadOnlyList<int> permisoIds, CancellationToken ct = default);

    /// <summary>Asigna el perfil a un usuario. Si ya lo tiene asignado, no duplica.</summary>
    Task<ServiceResult> AsignarAUsuarioAsync(int perfilId, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Retira el perfil de un usuario.</summary>
    Task<ServiceResult> QuitarDeUsuarioAsync(int perfilId, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Retorna los perfiles asignados a un usuario específico.</summary>
    Task<IReadOnlyList<PerfilDto>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>Elimina lógicamente el perfil (soft-delete).</summary>
    Task<ServiceResult> EliminarAsync(int perfilId, Guid usuarioModificacionId, CancellationToken ct = default);
}

using Ybridio.Application.DTOs.Seguridad;

namespace Ybridio.Application.Services.Autorizacion;

/// <summary>
/// Motor de autorización runtime del ERP.
/// Punto de entrada principal para todas las decisiones de acceso.
/// Encapsula la evaluación de permisos con semántica fluida: <c>Puede("salida.autorizar")</c>.
/// </summary>
/// <remarks>
/// Evaluación de permisos (orden de prioridad):
/// 1. Override explícito de usuario (UsuarioPermiso.Permitido = true/false)
/// 2. Permisos de perfiles asignados al usuario (UsuarioPerfil → PerfilPermiso)
/// 3. Permisos heredados del rol (RolPermiso)
/// Un denegado explícito (override false) siempre gana sobre cualquier otro nivel.
/// </remarks>
public interface IErpAuthorizationService
{
    /// <summary>
    /// Determina si el usuario de la sesión activa tiene el permiso indicado.
    /// Retorna false si no hay sesión autenticada.
    /// </summary>
    /// <param name="clave">Clave del permiso, e.g. <c>"salida.autorizar"</c>.</param>
    Task<bool> PuedeAsync(string clave, CancellationToken ct = default);

    /// <summary>
    /// Determina si un usuario específico tiene el permiso indicado.
    /// Útil para validaciones backend donde el userId no proviene de la sesión activa.
    /// </summary>
    Task<bool> PuedeAsync(Guid usuarioId, string clave, CancellationToken ct = default);

    /// <summary>
    /// Retorna todos los permisos efectivos del usuario de la sesión activa.
    /// Null si no hay sesión.
    /// </summary>
    Task<IReadOnlySet<string>?> ObtenerPermisosEfectivosAsync(CancellationToken ct = default);

    /// <summary>
    /// Retorna todos los permisos efectivos de un usuario específico.
    /// </summary>
    Task<IReadOnlySet<string>> ObtenerPermisosEfectivosAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Construye el contexto de seguridad completo del usuario de la sesión activa.
    /// Null si no hay sesión.
    /// </summary>
    Task<SecurityContextDto?> ObtenerContextoSeguridad(CancellationToken ct = default);
}

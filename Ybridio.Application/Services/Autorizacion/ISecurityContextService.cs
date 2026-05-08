using Ybridio.Application.DTOs.Seguridad;

namespace Ybridio.Application.Services.Autorizacion;

/// <summary>
/// Construye el snapshot de seguridad completo de un usuario:
/// roles, perfiles, permisos efectivos y scopes empresa/sucursal/almacén.
/// </summary>
public interface ISecurityContextService
{
    /// <summary>
    /// Retorna el contexto de seguridad del usuario de la sesión activa.
    /// Null si no hay sesión autenticada.
    /// </summary>
    Task<SecurityContextDto?> ObtenerContextoAsync(CancellationToken ct = default);

    /// <summary>
    /// Retorna el contexto de seguridad de un usuario específico.
    /// Null si el usuario no existe o está inactivo.
    /// </summary>
    Task<SecurityContextDto?> ObtenerContextoAsync(Guid usuarioId, CancellationToken ct = default);
}

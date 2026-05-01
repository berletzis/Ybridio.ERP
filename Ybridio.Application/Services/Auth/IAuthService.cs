using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Seguridad;

namespace Ybridio.Application.Services.Auth;

/// <summary>
/// Contrato para autenticación y gestión de sesión del usuario actual.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Valida credenciales y retorna el DTO del usuario si el login es correcto
    /// y el usuario está activo.
    /// </summary>
    Task<ServiceResult<UsuarioDto>> LoginAsync(LoginDto dto, CancellationToken ct = default);

    /// <summary>Obtiene el usuario por su ID.</summary>
    Task<ServiceResult<UsuarioDto>> ObtenerUsuarioPorIdAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>Valida que el usuario exista, esté activo y no esté borrado.</summary>
    Task<ServiceResult<UsuarioDto>> ValidarUsuarioActivoAsync(Guid usuarioId, CancellationToken ct = default);
}

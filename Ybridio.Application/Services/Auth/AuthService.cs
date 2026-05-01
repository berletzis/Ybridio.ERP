using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Application.Services.Auth;

/// <summary>
/// Implementación de autenticación usando UserManager de ASP.NET Core Identity.
/// No depende de contexto HTTP — apto para WinUI3.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AuthService> _logger;

    public AuthService(UserManager<ApplicationUser> userManager, ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<UsuarioDto>> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var opId = OperationContext.CurrentId;
        // Email no se registra en logs (dato personal)
        _logger.LogInformation("{OperationId} Intento de login recibido.", opId);

        try
        {
            var usuario = await _userManager.FindByEmailAsync(dto.Email);

            if (usuario is null || usuario.Borrado)
            {
                _logger.LogWarning("{OperationId} Login fallido: usuario no encontrado o eliminado.", opId);
                return ServiceResult<UsuarioDto>.Fail(
                    "Credenciales inválidas.",
                    ErrorCode.InvalidCredentials);
            }

            if (!usuario.Activo)
            {
                _logger.LogWarning("{OperationId} Login fallido: usuario {UsuarioId} inactivo.", opId, usuario.Id);
                return ServiceResult<UsuarioDto>.Fail(
                    "El usuario está inactivo. Contacta al administrador.",
                    ErrorCode.UserInactive);
            }

            var passwordValida = await _userManager.CheckPasswordAsync(usuario, dto.Password);
            if (!passwordValida)
            {
                _logger.LogWarning("{OperationId} Login fallido: contraseña incorrecta. Usuario:{UsuarioId}.", opId, usuario.Id);
                return ServiceResult<UsuarioDto>.Fail(
                    "Credenciales inválidas.",
                    ErrorCode.InvalidCredentials);
            }

            _logger.LogInformation("{OperationId} Login exitoso. Usuario:{UsuarioId}.", opId, usuario.Id);
            return ServiceResult<UsuarioDto>.Ok(MapToDto(usuario));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{OperationId} Error inesperado en login.", opId);
            return ServiceResult<UsuarioDto>.Fail("Error inesperado al iniciar sesión.", ErrorCode.Unknown);
        }
        finally
        {
            OperationContext.Clear();
        }
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<UsuarioDto>> ObtenerUsuarioPorIdAsync(
        Guid usuarioId, CancellationToken ct = default)
    {
        try
        {
            var usuario = await _userManager.FindByIdAsync(usuarioId.ToString());

            if (usuario is null || usuario.Borrado)
                return ServiceResult<UsuarioDto>.Fail(
                    "Usuario no encontrado.",
                    ErrorCode.NotFound);

            return ServiceResult<UsuarioDto>.Ok(MapToDto(usuario));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al obtener usuario {UsuarioId}.", usuarioId);
            return ServiceResult<UsuarioDto>.Fail("Error inesperado.", ErrorCode.Unknown);
        }
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<UsuarioDto>> ValidarUsuarioActivoAsync(
        Guid usuarioId, CancellationToken ct = default)
    {
        try
        {
            var usuario = await _userManager.FindByIdAsync(usuarioId.ToString());

            if (usuario is null || usuario.Borrado)
                return ServiceResult<UsuarioDto>.Fail(
                    "Usuario no encontrado.",
                    ErrorCode.NotFound);

            if (!usuario.Activo)
                return ServiceResult<UsuarioDto>.Fail(
                    "El usuario está inactivo.",
                    ErrorCode.UserInactive);

            return ServiceResult<UsuarioDto>.Ok(MapToDto(usuario));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al validar usuario {UsuarioId}.", usuarioId);
            return ServiceResult<UsuarioDto>.Fail("Error inesperado.", ErrorCode.Unknown);
        }
    }

    // ── mapeo interno ──────────────────────────────────────────────────────────

    private static UsuarioDto MapToDto(ApplicationUser u) =>
        new(u.Id, u.EmpresaId, u.Nombre, u.UserName!, u.Email, u.Activo);
}

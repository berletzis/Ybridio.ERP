using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Core;
using Ybridio.Application.DTOs.Seguridad;

namespace Ybridio.Application.Services.Seguridad;

public interface IUsuarioService
{
    Task<IReadOnlyList<UsuarioDto>> ListarPorEmpresaAsync(
        int empresaId, CancellationToken ct = default);

    Task<ServiceResult<UsuarioDto>> CrearAsync(
        CrearUsuarioDto dto, CancellationToken ct = default);

    Task<ServiceResult<UsuarioDto>> ActualizarAsync(
        Guid usuarioId, ActualizarUsuarioDto dto, Guid modificadoPor, CancellationToken ct = default);

    Task<ServiceResult> CambiarActivoAsync(
        Guid usuarioId, bool activo, Guid modificadoPor, CancellationToken ct = default);

    /// <summary>Sucursales actualmente asignadas al usuario.</summary>
    Task<IReadOnlyList<SucursalDto>> ListarSucursalesAsync(
        Guid usuarioId, CancellationToken ct = default);

    /// <summary>Reemplaza la lista completa de tiendas asignadas al usuario.</summary>
    Task<ServiceResult> AsignarSucursalesAsync(
        Guid usuarioId, IReadOnlyList<int> tiendaIds, CancellationToken ct = default);

    /// <summary>Roles (nombres) actualmente asignados al usuario.</summary>
    Task<IReadOnlyList<string>> ListarRolesAsync(
        Guid usuarioId, CancellationToken ct = default);

    /// <summary>Reemplaza los roles asignados al usuario.</summary>
    Task<ServiceResult> AsignarRolesAsync(
        Guid usuarioId, IReadOnlyList<string> roles, CancellationToken ct = default);
}

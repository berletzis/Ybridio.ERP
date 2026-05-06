using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Core;

namespace Ybridio.Application.Services.Sucursal;

public interface ISucursalService
{
    /// <summary>Lista todas las sucursales activas de una empresa.</summary>
    Task<IReadOnlyList<SucursalDto>> ListarPorEmpresaAsync(
        int empresaId, CancellationToken ct = default);

    /// <summary>Lista las sucursales a las que el usuario tiene acceso asignado.</summary>
    Task<IReadOnlyList<SucursalDto>> ListarPorUsuarioAsync(
        Guid usuarioId, CancellationToken ct = default);

    Task<ServiceResult<SucursalDto>> CrearAsync(
        int empresaId, string nombre, Guid usuarioId, CancellationToken ct = default);

    Task<ServiceResult<SucursalDto>> ActualizarAsync(
        int sucursalId, string nombre, Guid usuarioId, CancellationToken ct = default);

    Task<ServiceResult> EliminarAsync(
        int sucursalId, Guid usuarioId, CancellationToken ct = default);
}

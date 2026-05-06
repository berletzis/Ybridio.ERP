using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Seguridad;

namespace Ybridio.Application.Services.Seguridad;

public interface IRolService
{
    Task<IReadOnlyList<RolDto>> ListarAsync(CancellationToken ct = default);

    Task<ServiceResult<RolDto>> CrearAsync(
        string nombre, CancellationToken ct = default);

    Task<ServiceResult> EliminarAsync(
        Guid rolId, CancellationToken ct = default);
}

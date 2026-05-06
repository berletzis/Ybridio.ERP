using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Core;

namespace Ybridio.Application.Services.Empresa;

public interface IEmpresaService
{
    Task<ServiceResult<EmpresaDto>> ObtenerPorIdAsync(
        int empresaId, CancellationToken ct = default);

    Task<ServiceResult<EmpresaDto>> ActualizarAsync(
        int empresaId, UpsertEmpresaDto dto, Guid usuarioId, CancellationToken ct = default);
}

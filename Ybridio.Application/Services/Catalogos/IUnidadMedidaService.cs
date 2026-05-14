using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Catalogos;

namespace Ybridio.Application.Services.Catalogos;

/// <summary>Gestiona el catálogo de unidades de medida (Pieza, Kg, Metro, Litro, etc.).</summary>
public interface IUnidadMedidaService
{
    Task<IReadOnlyList<UnidadMedidaDto>> ListarAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UnidadMedidaDto>> ListarTodosAsync(CancellationToken ct = default);

    /// <summary>Crea o actualiza. Id == 0 → crear; Id > 0 → actualizar.</summary>
    Task<ServiceResult<UnidadMedidaDto>> GuardarAsync(int id, UpsertUnidadMedidaDto dto, CancellationToken ct = default);

    Task<ServiceResult> EliminarAsync(int id, CancellationToken ct = default);
}

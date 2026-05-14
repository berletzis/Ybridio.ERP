using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Catalogos;

namespace Ybridio.Application.Services.Catalogos;

/// <summary>
/// Gestiona el catálogo de tipos de producto (Inventariable, Servicio, No inventariable, etc.).
/// </summary>
public interface ITipoProductoService
{
    Task<IReadOnlyList<TipoProductoDto>> ListarAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TipoProductoDto>> ListarTodosAsync(CancellationToken ct = default);

    /// <summary>Crea o actualiza. Id == 0 → crear; Id > 0 → actualizar.</summary>
    Task<ServiceResult<TipoProductoDto>> GuardarAsync(int id, UpsertTipoProductoDto dto, CancellationToken ct = default);

    Task<ServiceResult> EliminarAsync(int id, CancellationToken ct = default);
}

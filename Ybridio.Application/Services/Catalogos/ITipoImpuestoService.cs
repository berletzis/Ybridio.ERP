using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Catalogos;

namespace Ybridio.Application.Services.Catalogos;

/// <summary>
/// Gestiona el catálogo de tipos de impuesto (IVA 16%, IVA 8%, Exento, etc.)
/// para la empresa en sesión. Reemplaza el hardcode de FiscalConstants.TasaIvaEstandar
/// para operaciones donde la tasa debe venir de configuración.
/// </summary>
public interface ITipoImpuestoService
{
    /// <summary>Lista todos los tipos de impuesto activos de la empresa.</summary>
    Task<IReadOnlyList<TipoImpuestoDto>> ListarAsync(CancellationToken ct = default);

    /// <summary>Lista todos (activos e inactivos) para administración.</summary>
    Task<IReadOnlyList<TipoImpuestoDto>> ListarTodosAsync(CancellationToken ct = default);

    /// <summary>Crea o actualiza un tipo de impuesto. Id == 0 → crear; Id > 0 → actualizar.</summary>
    Task<ServiceResult<TipoImpuestoDto>> GuardarAsync(int id, UpsertTipoImpuestoDto dto, CancellationToken ct = default);

    /// <summary>Elimina lógicamente un tipo de impuesto.</summary>
    Task<ServiceResult> EliminarAsync(int id, CancellationToken ct = default);
}

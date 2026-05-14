using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Catalogos;

namespace Ybridio.Application.Services.Configuracion;

/// <summary>
/// Gestiona el catálogo de Otros Cargos (accesorios documentales como Flete, Maniobras, Seguro).
/// Estos cargos NO son productos inventariables; son cargos adicionales en documentos comerciales.
/// </summary>
public interface IOtroCargoService
{
    /// <summary>Lista todos los cargos activos de la empresa, ordenados por OrdenVisual y Nombre.</summary>
    Task<IReadOnlyList<OtroCargoDto>> ListarAsync(CancellationToken ct = default);

    /// <summary>Lista todos los cargos (activos e inactivos) para administración.</summary>
    Task<IReadOnlyList<OtroCargoDto>> ListarTodosAsync(CancellationToken ct = default);

    /// <summary>Crea o actualiza un cargo accesorio.</summary>
    Task<ServiceResult<OtroCargoDto>> GuardarAsync(GuardarOtroCargoDto dto, CancellationToken ct = default);

    /// <summary>Elimina lógicamente un cargo (soft delete).</summary>
    Task<ServiceResult> EliminarAsync(int id, CancellationToken ct = default);
}

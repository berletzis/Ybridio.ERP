using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Catalogos;

namespace Ybridio.Application.Services.Catalogos;

/// <summary>
/// Gestiona el catálogo de series documentales (configuración de folios) para la empresa en sesión.
/// Este servicio es para administración (CRUD) — la generación runtime de folios
/// corresponde a <see cref="Ybridio.Application.Services.Folios.IFolioGeneratorService"/>.
/// </summary>
public interface ISerieDocumentoService
{
    /// <summary>Lista todas las series documentales activas de la empresa.</summary>
    Task<IReadOnlyList<SerieDocumentoDto>> ListarAsync(CancellationToken ct = default);

    /// <summary>Lista todas (activas e inactivas) para administración.</summary>
    Task<IReadOnlyList<SerieDocumentoDto>> ListarTodasAsync(CancellationToken ct = default);

    /// <summary>Crea o actualiza una serie documental. Id == 0 → crear.</summary>
    Task<ServiceResult<SerieDocumentoDto>> GuardarAsync(GuardarSerieDocumentoDto dto, CancellationToken ct = default);

    /// <summary>Elimina lógicamente una serie (soft delete). No elimina folios ya generados.</summary>
    Task<ServiceResult> EliminarAsync(int id, CancellationToken ct = default);
}

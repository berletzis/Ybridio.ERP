using System.Threading;
using System.Threading.Tasks;
using Ybridio.Domain.Catalogos;

namespace Ybridio.Application.Services.Folios;

/// <summary>
/// Motor institucional de generación de folios documentales.
///
/// Shared Sequence/Folio Pattern:
/// - La generación es ATÓMICA en BD para garantizar unicidad bajo concurrencia.
/// - Retorna null si no hay SerieDocumento configurada para el tipo de documento.
/// - NO debe invocarse desde UI/ViewModels — solo desde servicios de Application.
/// - Document Identity Rule: cada tipo de documento tiene su propio consecutivo.
///
/// Anti-patterns prohibidos:
/// - NUNCA generar folios en ViewModels o Pages
/// - NUNCA usar ParametroGlobal como consecutivo runtime
/// - NUNCA reutilizar folios en conversiones documentales (COT→PED→VTA son distintos)
/// </summary>
public interface IFolioGeneratorService
{
    /// <summary>
    /// Genera el siguiente folio para el tipo de documento indicado, de forma atómica y concurrente.
    /// Si no existe SerieDocumento activa configurada para la empresa/tipo, retorna null.
    /// </summary>
    /// <param name="empresaId">Empresa activa en sesión.</param>
    /// <param name="tipo">Tipo de documento para el que se genera el folio.</param>
    /// <param name="sucursalId">Sucursal activa. Primero busca serie específica, luego global (SucursalId = null).</param>
    /// <returns>Folio formateado (ej: "COT-000001"), o null si no hay serie configurada.</returns>
    Task<string?> GenerarFolioAsync(
        int empresaId,
        TipoDocumentoSerie tipo,
        int? sucursalId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene el folio que SE GENERARÍA a continuación, SIN consumir el consecutivo.
    /// Útil para mostrar una vista previa en formularios de creación.
    /// </summary>
    Task<string?> ObtenerFolioSiguienteAsync(
        int empresaId,
        TipoDocumentoSerie tipo,
        int? sucursalId = null,
        CancellationToken ct = default);
}

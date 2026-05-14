using Ybridio.Application.DTOs.Directorio;

namespace Ybridio.Application.Services.Directorio;

/// <summary>
/// Contrato para búsqueda en el Directorio comercial (ADR-038).
/// El Directorio (Persona / EmpresaComercial) es el source of truth para selección UI.
/// NO requiere RelacionComercial preexistente — ésta se crea bajo demanda al guardar.
/// </summary>
public interface IDirectorioService
{
    /// <summary>
    /// Busca entidades del directorio (Persona y EmpresaComercial) por nombre, RFC o email.
    /// Devuelve resultados ordenados y paginados para el selector institucional.
    /// Funciona sin RelacionComercial preexistente.
    /// </summary>
    /// <param name="empresaId">Empresa tenant para la búsqueda multiempresa.</param>
    /// <param name="termino">Texto libre: nombre, apellidos, razón social, RFC, email, teléfono.</param>
    /// <param name="ct">Token de cancelación (ADR-026).</param>
    Task<IReadOnlyList<DirectorioSelectorDto>> BuscarParaSelectorAsync(
        int empresaId, string termino, CancellationToken ct = default);

    /// <summary>
    /// Construye un <see cref="DirectorioSelectorDto"/> completamente hidratado a partir de un
    /// <c>RelacionComercialId</c>. Resuelve el vínculo a su entidad real (Persona o EmpresaComercial)
    /// y devuelve nombre, tipo, email, teléfono y RFC correctos.
    /// </summary>
    /// <remarks>
    /// Selector DTO Hydration Rule: el DTO NUNCA se construye manualmente con datos parciales.
    /// Este método es el Single Source of Truth para hidratación del chip del selector en edición,
    /// rehost y detach mode.
    /// </remarks>
    /// <param name="relacionComercialId">ID de la RelacionComercial a resolver.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>DTO completo, o <c>null</c> si la relación no existe o no tiene entidad vinculada.</returns>
    Task<DirectorioSelectorDto?> ObtenerDtoParaSelectorAsync(
        int relacionComercialId, CancellationToken ct = default);
}

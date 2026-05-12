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
}

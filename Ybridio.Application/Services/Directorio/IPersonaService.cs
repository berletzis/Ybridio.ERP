using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Directorio;

namespace Ybridio.Application.Services.Directorio;

/// <summary>
/// Contrato para gestión de Personas del directorio.
/// Toda operación de escritura valida permiso <c>directorio.editar</c>.
/// El listado valida <c>directorio.ver</c>.
/// </summary>
public interface IPersonaService
{
    /// <summary>Lista todas las personas activas de la empresa.</summary>
    Task<IReadOnlyList<PersonaDto>> ListarPorEmpresaAsync(int empresaId, CancellationToken ct = default);

    /// <summary>Busca personas por nombre, apellidos, RFC o email.</summary>
    Task<IReadOnlyList<PersonaDto>> BuscarAsync(int empresaId, string termino, CancellationToken ct = default);

    /// <summary>Obtiene una persona por ID.</summary>
    Task<ServiceResult<PersonaDto>> ObtenerPorIdAsync(int personaId, CancellationToken ct = default);

    /// <summary>Crea una persona nueva.</summary>
    Task<ServiceResult<PersonaDto>> CrearAsync(CrearPersonaDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Actualiza datos de una persona.</summary>
    Task<ServiceResult<PersonaDto>> ActualizarAsync(int personaId, ActualizarPersonaDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Soft-delete de una persona.</summary>
    Task<ServiceResult> EliminarAsync(int personaId, Guid usuarioId, CancellationToken ct = default);
}

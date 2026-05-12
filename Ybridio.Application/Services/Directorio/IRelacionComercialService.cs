using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Directorio;
using Ybridio.Domain.Catalogos;

namespace Ybridio.Application.Services.Directorio;

/// <summary>
/// Contrato para gestión de RelacionesComerciales del directorio.
/// Toda operación de escritura valida permiso <c>directorio.editar</c>.
/// El listado valida <c>directorio.ver</c>.
/// </summary>
public interface IRelacionComercialService
{
    /// <summary>Lista todas las relaciones comerciales activas de la empresa, opcionalmente filtrando por tipo.</summary>
    Task<IReadOnlyList<RelacionComercialDto>> ListarPorEmpresaAsync(
        int empresaId, TipoRelacionComercial? tipo = null, CancellationToken ct = default);

    /// <summary>Busca relaciones por nombre del socio (persona o empresa), RFC o email.</summary>
    Task<IReadOnlyList<RelacionComercialDto>> BuscarAsync(int empresaId, string termino, CancellationToken ct = default);

    /// <summary>Lista compacta para selectores de cotización / venta.</summary>
    Task<IReadOnlyList<RelacionComercialSelectorDto>> ListarParaSelectorAsync(
        int empresaId, string? termino = null, CancellationToken ct = default);

    /// <summary>Obtiene una relación comercial por ID.</summary>
    Task<ServiceResult<RelacionComercialDto>> ObtenerPorIdAsync(int relacionId, CancellationToken ct = default);

    /// <summary>Crea una relación comercial vinculada a una Persona.</summary>
    Task<ServiceResult<RelacionComercialDto>> CrearParaPersonaAsync(
        CrearRelacionComercialPersonaDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Crea una relación comercial vinculada a una EmpresaComercial.</summary>
    Task<ServiceResult<RelacionComercialDto>> CrearParaEmpresaAsync(
        CrearRelacionComercialEmpresaDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Actualiza tipo, límite de crédito y observaciones de una relación comercial.</summary>
    Task<ServiceResult<RelacionComercialDto>> ActualizarAsync(
        int relacionId, ActualizarRelacionComercialDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Soft-delete de una relación comercial.</summary>
    Task<ServiceResult> EliminarAsync(int relacionId, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene la RelacionComercial existente para la entidad de directorio dada, o la crea si no existe (ADR-038).
    /// Garantiza que el vínculo operativo existe antes de persistir un documento comercial.
    /// </summary>
    /// <param name="empresaId">Empresa tenant.</param>
    /// <param name="entidad">DTO del selector de directorio con PersonaId o EmpresaComercialId.</param>
    /// <param name="usuarioId">Usuario que genera la creación si aplica.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>ID de la RelacionComercial existente o recién creada.</returns>
    Task<ServiceResult<int>> GetOrCreateAsync(
        int empresaId, DirectorioSelectorDto entidad, Guid usuarioId, CancellationToken ct = default);
}

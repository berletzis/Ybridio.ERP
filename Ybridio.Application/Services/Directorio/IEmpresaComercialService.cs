using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Directorio;

namespace Ybridio.Application.Services.Directorio;

/// <summary>
/// Contrato para gestión de EmpresasComerciales del directorio.
/// Toda operación de escritura valida permiso <c>directorio.editar</c>.
/// El listado valida <c>directorio.ver</c>.
/// </summary>
public interface IEmpresaComercialService
{
    /// <summary>Lista todas las empresas comerciales activas de la empresa.</summary>
    Task<IReadOnlyList<EmpresaComercialDto>> ListarPorEmpresaAsync(int empresaId, CancellationToken ct = default);

    /// <summary>Busca empresas por razón social, nombre comercial o RFC.</summary>
    Task<IReadOnlyList<EmpresaComercialDto>> BuscarAsync(int empresaId, string termino, CancellationToken ct = default);

    /// <summary>Obtiene una empresa comercial por ID.</summary>
    Task<ServiceResult<EmpresaComercialDto>> ObtenerPorIdAsync(int empresaComercialId, CancellationToken ct = default);

    /// <summary>Crea una empresa comercial nueva.</summary>
    Task<ServiceResult<EmpresaComercialDto>> CrearAsync(CrearEmpresaComercialDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Actualiza datos de una empresa comercial.</summary>
    Task<ServiceResult<EmpresaComercialDto>> ActualizarAsync(int empresaComercialId, ActualizarEmpresaComercialDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Soft-delete de una empresa comercial.</summary>
    Task<ServiceResult> EliminarAsync(int empresaComercialId, Guid usuarioId, CancellationToken ct = default);
}

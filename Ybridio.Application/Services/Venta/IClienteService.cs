using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Ventas;

namespace Ybridio.Application.Services.Venta;

/// <summary>
/// Contrato para gestión de clientes del Sales Core.
/// Toda operación de escritura valida permiso <c>cliente.crear</c> o <c>cliente.editar</c>.
/// El listado valida <c>cliente.ver</c>.
/// </summary>
public interface IClienteService
{
    /// <summary>Lista todos los clientes activos de la empresa. Valida cliente.ver.</summary>
    Task<IReadOnlyList<ClienteDto>> ListarPorEmpresaAsync(int empresaId, CancellationToken ct = default);

    /// <summary>Busca clientes por nombre, RFC o email. Valida cliente.ver.</summary>
    Task<IReadOnlyList<ClienteDto>> BuscarAsync(int empresaId, string termino, CancellationToken ct = default);

    /// <summary>Obtiene un cliente por ID. Valida cliente.ver.</summary>
    Task<ServiceResult<ClienteDto>> ObtenerPorIdAsync(int clienteId, CancellationToken ct = default);

    /// <summary>Crea un cliente nuevo. Valida cliente.crear.</summary>
    Task<ServiceResult<ClienteDto>> CrearAsync(CrearClienteDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Actualiza datos de un cliente. Valida cliente.editar.</summary>
    Task<ServiceResult<ClienteDto>> ActualizarAsync(int clienteId, ActualizarClienteDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Soft-delete de un cliente. Valida cliente.editar.</summary>
    Task<ServiceResult> EliminarAsync(int clienteId, Guid usuarioId, CancellationToken ct = default);
}

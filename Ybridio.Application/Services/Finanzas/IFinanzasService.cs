using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Finanzas;
using Ybridio.Domain.Finanzas;

namespace Ybridio.Application.Services.Finanzas;

/// <summary>
/// Contrato para gestión de gastos e ingresos operativos con enforcement de autorización.
/// Todos los métodos de escritura validan permiso <c>finanzas.crear</c>/<c>finanzas.editar</c>/<c>finanzas.eliminar</c>.
/// El listado valida <c>finanzas.ver</c>.
/// </summary>
public interface IFinanzasService
{
    /// <summary>Lista movimientos (gastos o ingresos) con filtro de fecha y tipo. Valida finanzas.ver.</summary>
    Task<ServiceResult<IReadOnlyList<MovimientoFinancieroDto>>> ListarAsync(
        int                      empresaId,
        TipoMovimientoFinanciero tipo,
        int?                     sucursalId = null,
        DateTime?                desde      = null,
        DateTime?                hasta      = null,
        CancellationToken        ct         = default);

    /// <summary>Obtiene un movimiento por ID. Valida finanzas.ver.</summary>
    Task<ServiceResult<MovimientoFinancieroDto>> ObtenerPorIdAsync(
        long id, CancellationToken ct = default);

    /// <summary>Crea un gasto o ingreso. Valida finanzas.crear.</summary>
    Task<ServiceResult<MovimientoFinancieroDto>> CrearAsync(
        CrearMovimientoFinancieroDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Actualiza un movimiento. Valida finanzas.editar.</summary>
    Task<ServiceResult<MovimientoFinancieroDto>> ActualizarAsync(
        long id, ActualizarMovimientoFinancieroDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Soft-delete de un movimiento. Valida finanzas.eliminar.</summary>
    Task<ServiceResult> EliminarAsync(long id, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Lista categorías activas de la empresa, filtradas opcionalmente por tipo.</summary>
    Task<IReadOnlyList<CategoriaFinancieraDto>> ListarCategoriasAsync(
        int empresaId, string? tipoAplicable = null, CancellationToken ct = default);
}

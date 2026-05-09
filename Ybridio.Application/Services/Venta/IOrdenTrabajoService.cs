using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Domain.Ventas;

namespace Ybridio.Application.Services.Venta;

/// <summary>
/// Contrato para gestión de órdenes de trabajo operativas PYME.
/// Las OT son ligeras — soportan talleres, servicios técnicos, reparaciones e instalaciones.
/// No implementan manufactura avanzada, MRP ni rutas de producción.
/// </summary>
public interface IOrdenTrabajoService
{
    /// <summary>Lista OTs de la empresa con filtros opcionales. Valida ordentrabajo.ver.</summary>
    Task<ServiceResult<IReadOnlyList<OTResumenDto>>> ListarAsync(
        int empresaId, EstatusOrdenTrabajo? estatus = null,
        DateTime? desde = null, DateTime? hasta = null, CancellationToken ct = default);

    /// <summary>Obtiene una OT con sus materiales. Valida ordentrabajo.ver.</summary>
    Task<ServiceResult<OrdenTrabajoDto>> ObtenerConMaterialesAsync(long id, CancellationToken ct = default);

    /// <summary>Crea una OT nueva en estado Nueva. Valida ordentrabajo.crear.</summary>
    Task<ServiceResult<OrdenTrabajoDto>> CrearAsync(CrearOrdenTrabajoDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Cambia el estado de la OT.
    /// Valida ordentrabajo.actualizar para transiciones normales; ordentrabajo.cerrar para Terminada/Entregada.
    /// </summary>
    Task<ServiceResult> CambiarEstatusAsync(long id, EstatusOrdenTrabajo nuevoEstatus, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Agrega un material o servicio a la OT.
    /// Recalcula OT.Total = SUM(materiales.Importe).
    /// Valida ordentrabajo.actualizar.
    /// </summary>
    Task<ServiceResult<DetalleLineaDto>> AgregarMaterialAsync(long otId, AgregarOTMaterialDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Elimina un material de la OT y recalcula el Total. Valida ordentrabajo.actualizar.</summary>
    Task<ServiceResult> EliminarMaterialAsync(long materialId, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Soft-delete de una OT. Valida ordentrabajo.actualizar.</summary>
    Task<ServiceResult> EliminarAsync(long id, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Actualiza el encabezado de una OT existente (sin materiales).
    /// Los materiales se gestionan individualmente via AgregarMaterial/EliminarMaterial.
    /// Valida ordentrabajo.actualizar.
    /// </summary>
    Task<ServiceResult<OrdenTrabajoDto>> ActualizarAsync(
        long id, ActualizarOrdenTrabajoDto dto, Guid usuarioId, CancellationToken ct = default);
}

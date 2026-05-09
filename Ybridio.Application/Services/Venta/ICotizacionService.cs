using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Domain.Ventas;

namespace Ybridio.Application.Services.Venta;

/// <summary>
/// Contrato para gestión de cotizaciones.
/// Ciclo de vida: Borrador → Enviada → Aprobada → [origina Pedido] | Cancelada.
/// </summary>
public interface ICotizacionService
{
    /// <summary>Lista cotizaciones de la empresa en el rango de fechas. Valida cotizacion.ver.</summary>
    Task<ServiceResult<IReadOnlyList<CotizacionResumenDto>>> ListarAsync(
        int empresaId, DateTime? desde = null, DateTime? hasta = null, CancellationToken ct = default);

    /// <summary>Obtiene una cotización con sus detalles. Valida cotizacion.ver.</summary>
    Task<ServiceResult<CotizacionDto>> ObtenerConDetallesAsync(long id, CancellationToken ct = default);

    /// <summary>Crea una cotización en estado Borrador. Valida cotizacion.crear.</summary>
    Task<ServiceResult<CotizacionDto>> CrearAsync(CrearCotizacionDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Cambia el estado de la cotización.
    /// Valida cotizacion.editar para Enviada/Aprobada; cotizacion.cancelar para Cancelada.
    /// </summary>
    Task<ServiceResult> CambiarEstatusAsync(long id, EstatusCotizacion nuevoEstatus, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Soft-delete de una cotización. Valida cotizacion.cancelar.</summary>
    Task<ServiceResult> EliminarAsync(long id, Guid usuarioId, CancellationToken ct = default);

    // ── Document workflow ─────────────────────────────────────────────────────

    /// <summary>
    /// Actualiza el encabezado de una cotización existente (sin detalles).
    /// Valida cotizacion.editar. Los detalles se gestionan individualmente.
    /// </summary>
    Task<ServiceResult<CotizacionDto>> ActualizarAsync(
        long id, ActualizarCotizacionDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Agrega una línea de detalle a la cotización.
    /// Recalcula Subtotal y Total de la cotización.
    /// <para>Fórmula Importe: Cantidad × PrecioUnitario (persistido).</para>
    /// <para>Fórmula Total: SUM(detalles.Importe) (persistido).</para>
    /// Valida cotizacion.editar.
    /// </summary>
    Task<ServiceResult<DetalleLineaDto>> AgregarDetalleAsync(
        long cotizacionId, CrearDetalleLineaDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Elimina una línea de detalle y recalcula los totales de la cotización.
    /// Valida cotizacion.editar.
    /// </summary>
    Task<ServiceResult> EliminarDetalleAsync(long detalleId, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Convierte una cotización Aprobada en un Pedido nuevo.
    /// Copia detalles y datos del cliente. Valida pedido.crear.
    /// </summary>
    Task<ServiceResult<PedidoDto>> ConvertirAPedidoAsync(long cotizacionId, Guid usuarioId, CancellationToken ct = default);
}

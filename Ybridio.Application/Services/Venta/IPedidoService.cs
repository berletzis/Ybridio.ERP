using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Domain.Ventas;

namespace Ybridio.Application.Services.Venta;

/// <summary>
/// Contrato para gestión de pedidos.
/// Un pedido es el compromiso operacional del cliente. Puede originarse desde
/// una cotización aprobada o crearse directamente.
/// </summary>
public interface IPedidoService
{
    /// <summary>Lista pedidos de la empresa en el rango de fechas. Valida pedido.ver.</summary>
    Task<ServiceResult<IReadOnlyList<PedidoResumenDto>>> ListarAsync(
        int empresaId, DateTime? desde = null, DateTime? hasta = null, CancellationToken ct = default);

    /// <summary>Obtiene un pedido con sus detalles. Valida pedido.ver.</summary>
    Task<ServiceResult<PedidoDto>> ObtenerConDetallesAsync(long id, CancellationToken ct = default);

    /// <summary>Crea un pedido nuevo. Valida pedido.crear.</summary>
    Task<ServiceResult<PedidoDto>> CrearAsync(CrearPedidoDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Cambia el estado del pedido.
    /// Valida pedido.editar para avanzar estados; pedido.cancelar para cancelar.
    /// </summary>
    Task<ServiceResult> CambiarEstatusAsync(long id, EstatusPedido nuevoEstatus, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Soft-delete de un pedido. Valida pedido.cancelar.</summary>
    Task<ServiceResult> EliminarAsync(long id, Guid usuarioId, CancellationToken ct = default);

    // ── Document workflow ─────────────────────────────────────────────────────

    /// <summary>Actualiza el encabezado de un pedido. Valida pedido.editar.</summary>
    Task<ServiceResult<PedidoDto>> ActualizarAsync(
        long id, ActualizarPedidoDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Agrega una línea de detalle al pedido. Recalcula Total.
    /// Fórmula Importe: Cantidad × PrecioUnitario. Valida pedido.editar.
    /// </summary>
    Task<ServiceResult<DetalleLineaDto>> AgregarDetalleAsync(
        long pedidoId, CrearDetalleLineaDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Elimina una línea de detalle y recalcula Total del pedido. Valida pedido.editar.</summary>
    Task<ServiceResult> EliminarDetalleAsync(long detalleId, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Genera una Orden de Trabajo desde un pedido.
    /// Copia cliente y datos del pedido al encabezado de la OT. Valida ordentrabajo.crear.
    /// </summary>
    Task<ServiceResult<OrdenTrabajoDto>> GenerarOrdenTrabajoAsync(
        long pedidoId, string descripcionTrabajo, Guid usuarioId, CancellationToken ct = default);

    // ── Cargos — Commercial Charges Pattern ──────────────────────────────────

    /// <summary>
    /// Agrega un cargo accesorio al pedido (Flete, Maniobras, Seguro, etc.).
    /// Recalcula Total del pedido. Valida pedido.editar.
    /// </summary>
    Task<ServiceResult<PedidoCargoDto>> AgregarCargoAsync(
        long pedidoId, CrearPedidoCargoDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Elimina un cargo accesorio del pedido y recalcula el Total. Valida pedido.editar.</summary>
    Task<ServiceResult> EliminarCargoAsync(long cargoId, Guid usuarioId, CancellationToken ct = default);

    // ── Dimensión financiera — Anticipos ─────────────────────────────────────

    /// <summary>
    /// Registra un anticipo o pago parcial contra el pedido.
    /// Actualiza <c>AnticipoPagado</c> y <c>EstadoFinanciero</c> del pedido.
    /// Valida pedido.editar.
    /// </summary>
    Task<ServiceResult<AnticipoPedidoDto>> RegistrarAnticipoAsync(
        long pedidoId, RegistrarAnticipoDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Devuelve el historial de anticipos de un pedido. Valida pedido.ver.
    /// </summary>
    Task<ServiceResult<IReadOnlyList<AnticipoPedidoDto>>> ListarAnticiposAsync(
        long pedidoId, CancellationToken ct = default);

    /// <summary>
    /// Establece el monto mínimo de anticipo requerido para el pedido.
    /// Null elimina el requisito. Valida pedido.editar.
    /// </summary>
    Task<ServiceResult> EstablecerAnticipoRequeridoAsync(
        long pedidoId, decimal? monto, Guid usuarioId, CancellationToken ct = default);
}

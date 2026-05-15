using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Ventas;

namespace Ybridio.Application.Services.Venta;

/// <summary>
/// Contrato para el flujo de venta documental (diferente al POS).
/// Ciclo: Borrador → PendientePago → Pagada → Facturada → Entregada → Cerrada | Cancelada.
/// </summary>
public interface IVentaDocumentalService
{
    /// <summary>Lista ventas documentales de la empresa. Valida venta.ver.</summary>
    Task<ServiceResult<IReadOnlyList<VentaDocumentalResumenDto>>> ListarAsync(
        int               empresaId,
        DateTime?         desde = null,
        DateTime?         hasta = null,
        CancellationToken ct    = default);

    /// <summary>Obtiene una venta con detalles y pagos. Valida venta.ver.</summary>
    Task<ServiceResult<VentaDocumentalDto>> ObtenerConDetallesAsync(long ventaId, CancellationToken ct = default);

    /// <summary>
    /// Crea una venta en estado Borrador. No afecta inventario ni CxC.
    /// Valida venta.crear.
    /// </summary>
    Task<ServiceResult<VentaDocumentalDto>> CrearAsync(
        CrearVentaDocumentalDto dto,
        Guid                   usuarioId,
        CancellationToken      ct = default);

    /// <summary>
    /// Genera una venta documental desde un pedido confirmado.
    /// Copia cliente, detalles y observaciones del pedido. Valida venta.crear + pedido.ver.
    /// </summary>
    Task<ServiceResult<VentaDocumentalDto>> GenerarDesdePedidoAsync(
        long              pedidoId,
        Guid              usuarioId,
        CancellationToken ct = default);

    /// <summary>Actualiza el encabezado de una venta en estado Borrador. Valida venta.editar.</summary>
    Task<ServiceResult<VentaDocumentalDto>> ActualizarAsync(
        long                       id,
        ActualizarVentaDocumentalDto dto,
        Guid                       usuarioId,
        CancellationToken          ct = default);

    /// <summary>
    /// Confirma la venta: descuenta inventario por línea y, si TipoPago = Crédito, genera CxC.
    /// Cambia Estatus a Confirmada. Valida venta.confirmar.
    /// Fórmula: Total = SUM(detalles.Importe); SaldoPendiente = Total - TotalPagado.
    /// </summary>
    Task<ServiceResult<VentaDocumentalDto>> ConfirmarAsync(
        long              ventaId,
        int               almacenId,
        Guid              usuarioId,
        CancellationToken ct = default);

    /// <summary>
    /// Registra un pago parcial o total contra la venta.
    /// Actualiza Venta.TotalPagado. Si TipoPago = Crédito, actualiza la CxC correspondiente.
    /// Valida pago.registrar.
    /// </summary>
    Task<ServiceResult<PagoVentaDto>> RegistrarPagoAsync(
        RegistrarPagoVentaDto dto,
        Guid                  usuarioId,
        CancellationToken     ct = default);

    /// <summary>
    /// Cancela la venta. Permitido desde cualquier estado excepto Cerrada o ya Cancelada.
    /// Valida venta.cancelar.
    /// </summary>
    Task<ServiceResult> CancelarAsync(long ventaId, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Cierra formalmente la venta cuando saldo = 0 y entrega completa.
    /// Transiciona a <see cref="Ybridio.Domain.Ventas.EstatusVenta.Cerrada"/>. Valida venta.confirmar.
    /// </summary>
    Task<ServiceResult> CerrarAsync(long ventaId, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Agrega una línea de detalle a una venta en Borrador. Recalcula Subtotal y Total.
    /// Fórmula Importe: Cantidad × PrecioUnitario. Valida venta.editar.
    /// </summary>
    Task<ServiceResult<DetalleLineaDto>> AgregarDetalleAsync(
        long                 ventaId,
        CrearDetalleLineaDto dto,
        Guid                 usuarioId,
        CancellationToken    ct = default);

    /// <summary>Elimina una línea de detalle de una venta en Borrador. Valida venta.editar.</summary>
    Task<ServiceResult> EliminarDetalleAsync(long detalleId, Guid usuarioId, CancellationToken ct = default);
}

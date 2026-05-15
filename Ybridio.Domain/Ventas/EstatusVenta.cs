namespace Ybridio.Domain.Ventas;

/// <summary>
/// Ciclo de vida de un documento de venta generado desde el flujo documental.
/// Flujo: Borrador → PendientePago → Pagada → Facturada → Entregada → Cerrada | Cancelada.
/// Las ventas POS legacy quedan con estatus PendientePago(1) por compatibilidad.
/// </summary>
public enum EstatusVenta
{
    /// <summary>Venta creada pero no confirmada. Sin impacto en inventario ni CxC.</summary>
    Borrador       = 0,

    /// <summary>
    /// Venta confirmada con saldo pendiente. Inventario descontado. CxC generada si TipoPago = Crédito.
    /// Valor DB: 1 (compatible con registros legacy "Confirmada").
    /// </summary>
    PendientePago  = 1,

    /// <summary>Saldo = 0. Todos los pagos aplicados. Pendiente de facturación o entrega.</summary>
    Pagada         = 2,

    /// <summary>Factura emitida (CFDI u otro comprobante fiscal). Puede estar pendiente de entrega.</summary>
    Facturada      = 3,

    /// <summary>Entrega física completada. Pendiente de cierre formal si aplica.</summary>
    Entregada      = 4,

    /// <summary>
    /// Ciclo comercial cerrado: saldo = 0, entrega completa, sin acción pendiente.
    /// Estado terminal de cierre exitoso.
    /// </summary>
    Cerrada        = 5,

    /// <summary>Venta cancelada. Estado terminal. No se revierte inventario en V1 (requiere ajuste manual).</summary>
    Cancelada      = 9,
}

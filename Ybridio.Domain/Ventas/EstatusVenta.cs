namespace Ybridio.Domain.Ventas;

/// <summary>
/// Ciclo de vida de un documento de venta generado desde el flujo documental.
/// Las ventas POS legacy quedan con estatus Confirmada(1) por defecto en la migración.
/// </summary>
public enum EstatusVenta
{
    /// <summary>Venta creada pero no confirmada. Sin impacto en inventario ni CxC.</summary>
    Borrador   = 0,
    /// <summary>Venta confirmada. Inventario descontado. CxC generada si TipoPago = Crédito.</summary>
    Confirmada = 1,
    /// <summary>Venta cancelada. No se revierte inventario en V1 (requiere ajuste manual).</summary>
    Cancelada  = 9,
}

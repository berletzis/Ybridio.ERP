namespace Ybridio.Domain.Ventas;

/// <summary>
/// Estados del ciclo de vida de un pedido (compromiso operacional con el cliente).
/// Flujo esperado: Nuevo → Confirmado → EnProceso → Completado | Cancelado.
/// Un pedido Completado puede originar una Venta o una OrdenTrabajo.
/// </summary>
public enum EstatusPedido
{
    /// <summary>Pedido recién creado, pendiente de confirmar.</summary>
    Nuevo      = 0,

    /// <summary>Confirmado internamente, listo para procesarse.</summary>
    Confirmado = 1,

    /// <summary>En proceso de preparación, despacho o servicio.</summary>
    EnProceso  = 2,

    /// <summary>Cumplido. Listo para entrega o facturación.</summary>
    Completado = 3,

    /// <summary>Cancelado. Estado terminal.</summary>
    Cancelado  = 9,
}

namespace Ybridio.Domain.Ventas;

/// <summary>
/// Estados del ciclo de vida de un pedido (compromiso operacional con el cliente).
/// Flujo: Borrador → Autorizado → EnProceso → Parcial | Finalizado | Cancelado.
/// Un pedido Finalizado puede originar una Venta o una OrdenTrabajo.
/// </summary>
public enum EstatusPedido
{
    /// <summary>Pedido recién creado o en edición, pendiente de autorizar.</summary>
    Borrador   = 0,

    /// <summary>Autorizado internamente, listo para procesarse.</summary>
    Autorizado = 1,

    /// <summary>En proceso de preparación, despacho o servicio.</summary>
    EnProceso  = 2,

    /// <summary>Cumplimiento parcial: al menos una línea entregada pero no todas.</summary>
    Parcial    = 4,

    /// <summary>Cumplido completamente. Listo para entrega o facturación.</summary>
    Finalizado = 3,

    /// <summary>Cancelado. Estado terminal.</summary>
    Cancelado  = 9,
}

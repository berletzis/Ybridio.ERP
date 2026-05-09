namespace Ybridio.Domain.Ventas;

/// <summary>
/// Estados del ciclo de vida de una cotización.
/// Flujo esperado: Borrador → Enviada → Aprobada → (genera Pedido o Venta) | Cancelada.
/// </summary>
public enum EstatusCotizacion
{
    /// <summary>En edición, no enviada al cliente.</summary>
    Borrador  = 0,

    /// <summary>Enviada al cliente, pendiente de respuesta.</summary>
    Enviada   = 1,

    /// <summary>Aprobada por el cliente. Puede originar un Pedido.</summary>
    Aprobada  = 2,

    /// <summary>Rechazada o expirada. Estado terminal.</summary>
    Cancelada = 9,
}

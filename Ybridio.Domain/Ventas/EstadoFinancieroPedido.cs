namespace Ybridio.Domain.Ventas;

/// <summary>
/// Estado financiero de un Pedido — dimensión independiente del workflow operacional (<see cref="EstatusPedido"/>).
/// Controla el nivel de compromiso financiero del cliente con el pedido.
/// </summary>
public enum EstadoFinancieroPedido
{
    /// <summary>Sin pagos registrados.</summary>
    SinPago          = 0,

    /// <summary>Anticipo registrado pero menor al mínimo requerido.</summary>
    AnticipoParcial  = 1,

    /// <summary>Anticipo cubre o supera el mínimo requerido. Permite generar OT.</summary>
    AnticipoCompleto = 2,

    /// <summary>Pagos registrados sin anticipo requerido configurado (pago libre).</summary>
    ParcialmentePagado = 3,

    /// <summary>Total pagado >= Total del pedido. Cierre financiero.</summary>
    Liquidado        = 4,

    /// <summary>Pagos registrados superiores al total del pedido. Requiere revisión.</summary>
    SobrePagado      = 5,
}

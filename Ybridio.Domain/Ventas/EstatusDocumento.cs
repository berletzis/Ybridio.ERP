namespace Ybridio.Domain.Ventas;

/// <summary>
/// Estados comerciales del ciclo de vida de una cotización.
/// </summary>
/// <remarks>
/// Commercial Document Workflow Pattern:
///
///   Borrador → Aprobada → Convertida
///                  ↘         ↘
///                  Cancelada  (terminal)
///
/// REGLA: "Enviar" es acción operacional (email/PDF/auditoría) — NO modifica estado.
/// Una cotización Aprobada puede enviarse múltiples veces sin cambiar estado.
/// </remarks>
public enum EstatusCotizacion
{
    /// <summary>En edición — estado inicial. Puede ser Aprobada o Cancelada.</summary>
    Borrador   = 0,

    /// <summary>
    /// LEGACY — mantenido para compatibilidad con registros históricos en BD.
    /// El workflow actual NO genera este estado. Tratar como Aprobada en lógica de negocio.
    /// </summary>
    [System.Obsolete("Estado legacy. El nuevo workflow usa Borrador → Aprobada directamente. No usar en código nuevo.")]
    Enviada    = 1,

    /// <summary>
    /// Aprobada comercialmente. Puede originar un Pedido (→ Convertida) o ser Cancelada.
    /// Una cotización Aprobada puede enviarse (acción operacional) múltiples veces.
    /// </summary>
    Aprobada   = 2,

    /// <summary>
    /// Convertida a Pedido. Estado terminal — no admite más modificaciones de workflow.
    /// Se establece automáticamente al ejecutar ConvertirAPedidoAsync.
    /// </summary>
    Convertida = 3,

    /// <summary>Cancelada. Estado terminal — no admite modificaciones.</summary>
    Cancelada  = 9,
}

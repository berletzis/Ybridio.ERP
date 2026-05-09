namespace Ybridio.Domain.Ventas;

/// <summary>
/// Estados del ciclo de vida de una orden de trabajo (servicio, reparación, instalación).
/// Flujo típico: Nueva → EnProceso → Terminada → Entregada.
/// EsperandoMaterial es un estado intermedio que puede darse en cualquier momento.
/// </summary>
public enum EstatusOrdenTrabajo
{
    /// <summary>Recién creada, sin asignar o iniciar.</summary>
    Nueva             = 0,

    /// <summary>En ejecución activa por el responsable.</summary>
    EnProceso         = 1,

    /// <summary>Pausada esperando materiales o partes.</summary>
    EsperandoMaterial = 2,

    /// <summary>Trabajo terminado, pendiente de entrega al cliente.</summary>
    Terminada         = 3,

    /// <summary>Entregada al cliente. Estado terminal exitoso.</summary>
    Entregada         = 4,

    /// <summary>Cancelada. Estado terminal.</summary>
    Cancelada         = 9,
}

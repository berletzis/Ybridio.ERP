namespace Ybridio.WinUI.Services.Windowing;

/// <summary>
/// Define la estrategia de posicionamiento al abrir una ventana secundaria.
/// </summary>
public enum WindowPositionStrategy
{
    /// <summary>
    /// Centra la ventana respecto a la ventana principal (MainWindow).
    /// Comportamiento por defecto para formularios de detalle en el ERP.
    /// </summary>
    CenterOwner,

    /// <summary>
    /// Centra la ventana en el monitor activo.
    /// Útil cuando la ventana principal está maximizada.
    /// </summary>
    CenterScreen,

    /// <summary>
    /// Abre en cascada con un offset incremental respecto a la ventana principal.
    /// Útil para múltiples registros abiertos simultáneamente.
    /// </summary>
    Cascade,

    /// <summary>
    /// No aplica posicionamiento automático; el OS decide la posición inicial.
    /// </summary>
    Manual
}

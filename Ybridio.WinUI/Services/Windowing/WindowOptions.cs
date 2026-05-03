namespace Ybridio.WinUI.Services.Windowing;

/// <summary>
/// Opciones de configuración para ventanas administradas por <see cref="IWindowManager"/>.
/// Diseñadas para extensión futura sin romper la API existente.
/// </summary>
/// <example>
/// Uso en módulo de Productos:
/// <code>
/// new WindowOptions { Width = 900, Height = 700, PositionStrategy = WindowPositionStrategy.CenterOwner }
/// </code>
/// </example>
public sealed class WindowOptions
{
    /// <summary>Ancho de la ventana en píxeles. Por defecto 900.</summary>
    public double Width { get; init; } = 900;

    /// <summary>Alto de la ventana en píxeles. Por defecto 700.</summary>
    public double Height { get; init; } = 700;

    /// <summary>Ancho mínimo al redimensionar. Por defecto 480.</summary>
    public double MinWidth { get; init; } = 480;

    /// <summary>Alto mínimo al redimensionar. Por defecto 360.</summary>
    public double MinHeight { get; init; } = 360;

    /// <summary>
    /// Indica si el usuario puede redimensionar la ventana. Por defecto <see langword="true"/>.
    /// </summary>
    public bool IsResizable { get; init; } = true;

    /// <summary>
    /// Estrategia de posicionamiento inicial.
    /// Por defecto <see cref="WindowPositionStrategy.CenterOwner"/>.
    /// </summary>
    public WindowPositionStrategy PositionStrategy { get; init; } = WindowPositionStrategy.CenterOwner;

    /// <summary>
    /// Si <see langword="true"/>, activa y trae la ventana al frente al abrirse.
    /// Por defecto <see langword="true"/>.
    /// </summary>
    public bool ActivateOnOpen { get; init; } = true;

    /// <summary>
    /// Desplazamiento en píxeles entre ventanas en cascada.
    /// Solo aplica con <see cref="WindowPositionStrategy.Cascade"/>. Por defecto 32.
    /// </summary>
    public int CascadeOffset { get; init; } = 32;
}

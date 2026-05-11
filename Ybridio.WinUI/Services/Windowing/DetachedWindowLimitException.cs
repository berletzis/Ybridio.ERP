using System;

namespace Ybridio.WinUI.Services.Windowing;

/// <summary>
/// Excepción lanzada cuando se intenta abrir una ventana desacoplada (detached window)
/// excediendo el límite máximo permitido (Window Detach Mode — ADR-028).
/// Esta es una excepción operacional que debe manejarse en UI con mensaje claro al usuario.
/// </summary>
public sealed class DetachedWindowLimitException : InvalidOperationException
{
    /// <summary>
    /// Límite máximo de ventanas desacopladas permitidas simultáneamente.
    /// </summary>
    public int MaxAllowed { get; }

    /// <summary>
    /// Cantidad actual de ventanas desacopladas abiertas.
    /// </summary>
    public int CurrentCount { get; }

    public DetachedWindowLimitException(int maxAllowed, int currentCount)
        : base($"Límite alcanzado: máximo {maxAllowed} ventanas desacopladas simultáneas. Cierre alguna ventana antes de abrir otra. (Activas: {currentCount})")
    {
        MaxAllowed   = maxAllowed;
        CurrentCount = currentCount;
    }
}

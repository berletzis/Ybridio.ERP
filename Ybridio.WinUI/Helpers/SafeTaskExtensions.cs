using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Ybridio.WinUI.Helpers;

/// <summary>
/// Helpers para fire-and-forget seguro en WinUI3 (Fase 3 — Runtime Stability Y26).
/// Captura excepciones silenciosas y las propaga como mensajes de error observables.
/// </summary>
public static class SafeTaskExtensions
{
    /// <summary>
    /// Ejecuta una tarea fire-and-forget con captura de excepciones.
    /// Propaga el error al <paramref name="onError"/> callback si la tarea falla.
    /// </summary>
    /// <param name="task">Tarea a ejecutar en background.</param>
    /// <param name="onError">Callback invocado con el mensaje de error si la tarea lanza.</param>
    /// <param name="logger">Logger opcional para trazabilidad.</param>
    public static void FireAndForget(
        this Task task,
        Action<string>? onError = null,
        ILogger?        logger  = null)
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception is not null)
            {
                var msg = t.Exception.InnerException?.Message ?? t.Exception.Message;
                logger?.LogError(t.Exception, "[FireAndForget] Tarea falló: {Message}", msg);
                onError?.Invoke(msg);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    /// Versión genérica que acepta cualquier <see cref="ValueTask"/>.
    /// </summary>
    public static void FireAndForget(
        this ValueTask task,
        Action<string>? onError = null,
        ILogger?        logger  = null)
        => task.AsTask().FireAndForget(onError, logger);
}

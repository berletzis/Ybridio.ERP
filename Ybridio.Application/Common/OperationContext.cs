namespace Ybridio.Application.Common;

/// <summary>
/// Mantiene un identificador único por operación asíncrona para trazabilidad en logs.
/// Usa <see cref="AsyncLocal{T}"/> para que cada llamada asíncrona tenga su propio valor
/// sin interferir con otras operaciones concurrentes.
/// </summary>
public static class OperationContext
{
    private static readonly AsyncLocal<Guid?> _operationId = new();

    /// <summary>
    /// Identificador de la operación actual. Se genera automáticamente si no se ha asignado uno.
    /// </summary>
    public static Guid CurrentId
    {
        get => _operationId.Value ??= Guid.NewGuid();
        set => _operationId.Value = value;
    }

    /// <summary>
    /// Limpia el identificador al finalizar la operación para evitar reutilización accidental.
    /// Llamar siempre en un bloque <c>finally</c>.
    /// </summary>
    public static void Clear() => _operationId.Value = null;
}

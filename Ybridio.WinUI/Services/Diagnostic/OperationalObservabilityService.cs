using System;
using System.Collections.Generic;

namespace Ybridio.WinUI.Services.Diagnostic;

/// <summary>
/// Implementación Singleton del servicio de observabilidad operacional.
/// Mantiene un historial circular de las últimas <see cref="MaxHistory"/> operaciones.
/// Thread-safe para UI thread (todas las llamadas ocurren desde el dispatcher thread de WinUI).
/// </summary>
public sealed class OperationalObservabilityService : IOperationalObservabilityService
{
    private const int MaxHistory = 15;

    private readonly List<GridOperationContext> _history = new(capacity: MaxHistory);

    /// <inheritdoc/>
    public void Report(GridOperationContext context)
    {
        if (_history.Count >= MaxHistory)
            _history.RemoveAt(0);

        _history.Add(context);
    }

    /// <inheritdoc/>
    public GridOperationContext? GetLatest()
        => _history.Count > 0 ? _history[^1] : null;

    /// <inheritdoc/>
    public IReadOnlyList<GridOperationContext> GetHistory()
    {
        // Devuelve en orden descendente (más reciente primero)
        var result = new List<GridOperationContext>(_history.Count);
        for (var i = _history.Count - 1; i >= 0; i--)
            result.Add(_history[i]);
        return result;
    }

    /// <inheritdoc/>
    public void Clear() => _history.Clear();
}

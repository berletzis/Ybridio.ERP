using System.Collections.Generic;

namespace Ybridio.WinUI.Services.Diagnostic;

/// <summary>
/// Servicio de observabilidad operacional del ERP.
/// Los ViewModels reportan voluntariamente su contexto de consulta mediante <see cref="Report"/>.
/// El panel de diagnóstico consume la información sin que los módulos conozcan su existencia.
/// Desacoplado: ninguna capa Domain/Application/Infrastructure lo conoce.
/// </summary>
public interface IOperationalObservabilityService
{
    /// <summary>Registra el contexto de una operación de grid/consulta.</summary>
    void Report(GridOperationContext context);

    /// <summary>Devuelve el contexto de la última operación registrada, o null si no hay ninguna.</summary>
    GridOperationContext? GetLatest();

    /// <summary>Devuelve las últimas operaciones registradas (orden cronológico descendente).</summary>
    IReadOnlyList<GridOperationContext> GetHistory();

    /// <summary>Limpia el historial de operaciones (e.g., al hacer logout).</summary>
    void Clear();
}

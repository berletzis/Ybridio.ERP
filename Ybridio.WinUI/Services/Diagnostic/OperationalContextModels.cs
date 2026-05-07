using System;
using System.Collections.Generic;

namespace Ybridio.WinUI.Services.Diagnostic;

/// <summary>Estado de un filtro en una consulta operacional.</summary>
public enum FilterState
{
    /// <summary>Filtro aplicado correctamente.</summary>
    Applied,
    /// <summary>Filtro omitido intencionalmente — es el comportamiento esperado para este módulo.</summary>
    OmittedExpected,
    /// <summary>Filtro faltante — debería haberse aplicado pero no lo fue.</summary>
    Missing
}

/// <summary>
/// Detalle del estado de un filtro individual en una consulta.
/// </summary>
public sealed record FilterDetail(
    FilterState State,
    string?     Value = null,
    string?     Note  = null)
{
    public string Icon => State switch
    {
        FilterState.Applied         => "✔",
        FilterState.OmittedExpected => "⚠",
        FilterState.Missing         => "❌",
        _                           => "?"
    };

    public string Display => State switch
    {
        FilterState.Applied         => $"✔  Aplicado{(Value is not null ? $" = {Value}" : "")}",
        FilterState.OmittedExpected => $"⚠  Omitido — {Note ?? "N/A para este módulo"}",
        FilterState.Missing         => $"❌  Faltante — {Note ?? "debería aplicarse"}",
        _                           => "?"
    };
}

/// <summary>
/// Snapshot inmutable de una operación de grid/consulta en el ERP.
/// Captura qué módulo consultó, bajo qué contexto, con qué filtros y cuántos registros retornó.
/// Liviano: no intercepta SQL — es metadata contextual registrada por el ViewModel voluntariamente.
/// </summary>
public sealed record GridOperationContext(
    // ── Origen ───────────────────────────────────────────────────────────────
    string  Module,
    string  SubModule,
    string  ViewModel,
    string  Entity,
    // ── Resultado ─────────────────────────────────────────────────────────────
    int     RecordCount,
    TimeSpan Duration,
    // ── Filtros aplicados ─────────────────────────────────────────────────────
    FilterDetail EmpresaFilter,
    FilterDetail SucursalFilter,
    FilterDetail AlmacenFilter,
    FilterDetail SoftDeleteFilter,
    // ── Estado de controles ───────────────────────────────────────────────────
    string?  SearchTerm,
    bool     SoloActivos,
    string?  CategoriaFiltro,
    string?  FiltroTemporal,
    // ── Notas adicionales ─────────────────────────────────────────────────────
    IReadOnlyList<string> Notes,
    DateTime Timestamp
)
{
    /// <summary>Hora formateada para el historial del panel (HH:mm:ss).</summary>
    public string TimeDisplay => Timestamp.ToString("HH:mm:ss");

    /// <summary>Duración formateada para el historial del panel.</summary>
    public string DurationDisplay => $"{Duration.TotalMilliseconds:F0} ms";
}

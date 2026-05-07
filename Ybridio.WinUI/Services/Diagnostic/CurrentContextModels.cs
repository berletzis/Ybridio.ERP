using System;

namespace Ybridio.WinUI.Services.Diagnostic;

/// <summary>
/// Contexto operacional actualmente visible en el ERP.
/// Refleja el estado VIVO del módulo/ViewModel activo en tiempo real.
/// A diferencia de <see cref="GridOperationContext"/> (snapshot de operación completada),
/// este registro se actualiza continuamente mientras el módulo está visible.
/// </summary>
public sealed record CurrentOperationalContext(
    // ── Identificación del contexto ───────────────────────────────────────────
    string  Module,
    string? SubModule,
    string? ViewModel,
    string? Entity,
    // ── Estado del grid ───────────────────────────────────────────────────────
    int     RecordCount,
    string? SearchTerm,
    bool    SoloActivos,
    string? CategoriaFiltro,
    string? FiltroTemporal,
    // ── Filtros activos ───────────────────────────────────────────────────────
    FilterDetail? EmpresaFilter,
    FilterDetail? SucursalFilter,
    FilterDetail? AlmacenFilter,
    FilterDetail? SoftDeleteFilter,
    // ── Metadata ─────────────────────────────────────────────────────────────
    /// <summary>"ModuleFrame" (navegación sidebar) o "WorkspaceTab" (workspace item).</summary>
    string   Source,
    DateTime UpdatedAt
)
{
    /// <summary>Tiempo transcurrido desde la última actualización del contexto.</summary>
    public string AgeDisplay
    {
        get
        {
            var age = DateTime.Now - UpdatedAt;
            return age.TotalMinutes < 1
                ? $"{(int)age.TotalSeconds}s"
                : $"{(int)age.TotalMinutes}m {age.Seconds:D2}s";
        }
    }

    /// <summary>Indica si el contexto tiene información de ViewModel (no solo de módulo).</summary>
    public bool HasViewModelContext => ViewModel is not null;
}

/// <summary>
/// Entrada del historial de módulos navegados en la sesión actual.
/// Se crea/actualiza cada vez que el usuario navega a un módulo vía sidebar o sub-tab.
/// Un módulo puede tener solo una entrada (se actualiza al revisitarlo).
/// </summary>
public sealed record ModuleNavigationEntry(
    string   Module,
    string?  SubModule,
    DateTime NavigatedAt)
{
    /// <summary>Representación legible: "Inventario › Produtos" o "Dashboard".</summary>
    public string Display =>
        SubModule is not null ? $"{Module} › {SubModule}" : Module;

    /// <summary>Tiempo transcurrido desde la última visita al módulo.</summary>
    public string AgeDisplay
    {
        get
        {
            var age = DateTime.Now - NavigatedAt;
            return age.TotalMinutes < 1
                ? $"{(int)age.TotalSeconds}s"
                : $"{(int)age.TotalMinutes}m {age.Seconds:D2}s";
        }
    }
}

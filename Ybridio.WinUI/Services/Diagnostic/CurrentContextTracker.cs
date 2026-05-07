using System;
using System.Collections.Generic;
using System.Linq;

namespace Ybridio.WinUI.Services.Diagnostic;

/// <summary>
/// Implementación Singleton del rastreador de contexto operacional activo.
///
/// Reglas para SetModuleContext:
///  • Mismo módulo + mismo subMódulo → preservar contexto ViewModel (no degradar)
///  • Mismo módulo + distinto subMódulo → reset a parcial (nueva vista interna)
///  • Distinto módulo → siempre reset a parcial
///
/// Historial: un entry por módulo, actualizado cada vez que se visita.
/// Refleja qué módulos ha abierto el usuario en la sesión actual.
/// </summary>
public sealed class CurrentContextTracker : ICurrentContextTracker
{
    private CurrentOperationalContext? _current;

    // Historial de navegación: un entry por módulo-key (actualize on revisit)
    private readonly Dictionary<string, ModuleNavigationEntry> _history = new();
    private string? _activeModuleKey;

    // ── ICurrentContextTracker ────────────────────────────────────────────────

    /// <inheritdoc/>
    public string? ActiveModuleKey => _activeModuleKey;

    /// <inheritdoc/>
    public void SetModuleContext(string module, string? subModule = null)
    {
        // Actualizar historial: un entry por módulo, sobrescribiendo al revisitar
        _history[module] = new ModuleNavigationEntry(module, subModule, DateTime.Now);
        _activeModuleKey = module;

        // Actualizar contexto activo según reglas de degradación:
        if (_current?.Module == module && _current.SubModule == subModule)
            return; // Mismo módulo + mismo subMódulo → preservar contexto ViewModel completo

        // Cambio de subMódulo (dentro del mismo módulo) o cambio de módulo → reset a parcial
        _current = CreatePartialContext(module, subModule);
    }

    /// <inheritdoc/>
    public void SetViewModelContext(CurrentOperationalContext context)
        => _current = context;

    /// <inheritdoc/>
    public CurrentOperationalContext? GetCurrent() => _current;

    /// <inheritdoc/>
    public IReadOnlyList<ModuleNavigationEntry> GetNavigationHistory()
        => _history.Values.OrderByDescending(e => e.NavigatedAt).ToList();

    /// <inheritdoc/>
    public void Clear()
    {
        _current        = null;
        _activeModuleKey = null;
        _history.Clear();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CurrentOperationalContext CreatePartialContext(string module, string? subModule) =>
        new(
            Module:           module,
            SubModule:        subModule,
            ViewModel:        null,
            Entity:           null,
            RecordCount:      0,
            SearchTerm:       null,
            SoloActivos:      false,
            CategoriaFiltro:  null,
            FiltroTemporal:   null,
            EmpresaFilter:    null,
            SucursalFilter:   null,
            AlmacenFilter:    null,
            SoftDeleteFilter: null,
            Source:           "ModuleFrame",
            UpdatedAt:        DateTime.Now
        );
}

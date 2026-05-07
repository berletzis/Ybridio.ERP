using System.Collections.Generic;

namespace Ybridio.WinUI.Services.Diagnostic;

/// <summary>
/// Rastrea el contexto operacional actualmente visible en el ERP.
/// Diferente de <see cref="IOperationalObservabilityService"/> que rastrea operaciones completadas:
/// este servicio refleja el estado VIVO del módulo/ViewModel activo.
/// También mantiene el historial de módulos visitados en la sesión actual.
/// </summary>
public interface ICurrentContextTracker
{
    /// <summary>
    /// Registra el módulo activo cuando el usuario navega (sin ViewModel aún).
    /// Llamado por ShellPage al navegar el ModuleFrame y por InventarioPage al cambiar sub-tab.
    /// Solo resetea el contexto activo si cambia módulo o subMódulo.
    /// Siempre actualiza el historial de navegación.
    /// </summary>
    void SetModuleContext(string module, string? subModule = null);

    /// <summary>
    /// Registra el contexto completo con datos del ViewModel y el grid actual.
    /// Llamado por ViewModels al cargar o refrescar datos.
    /// Sobrescribe cualquier contexto parcial previo.
    /// </summary>
    void SetViewModelContext(CurrentOperationalContext context);

    /// <summary>Devuelve el contexto actualmente visible, o null si no hay ninguno.</summary>
    CurrentOperationalContext? GetCurrent();

    /// <summary>
    /// Devuelve el historial de módulos navegados en la sesión, uno por módulo
    /// (actualizado al revisitar), ordenado por visita más reciente primero.
    /// Cada llamada a SetModuleContext crea o actualiza la entrada del módulo.
    /// </summary>
    IReadOnlyList<ModuleNavigationEntry> GetNavigationHistory();

    /// <summary>Módulo actualmente activo (el último que recibió SetModuleContext).</summary>
    string? ActiveModuleKey { get; }

    /// <summary>Limpia el contexto activo e historial (e.g., al hacer logout).</summary>
    void Clear();
}

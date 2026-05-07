namespace Ybridio.WinUI.Services.Diagnostic;

/// <summary>
/// Interfaz opcional que Pages/ViewModels pueden implementar para reportar
/// su contexto vivo al <see cref="ICurrentContextTracker"/> sin que
/// el módulo conocedor (e.g., InventarioPage) necesite referenciar tipos concretos.
/// </summary>
public interface ILiveContextReporter
{
    /// <summary>
    /// Reporta inmediatamente el contexto operacional actual al tracker.
    /// No ejecuta queries — usa el estado en memoria del ViewModel.
    /// </summary>
    void ReportLiveContext();
}

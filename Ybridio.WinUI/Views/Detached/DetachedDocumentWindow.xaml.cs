using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Ybridio.WinUI.Views.Detached;

/// <summary>
/// Ventana OS real independiente para Document Surfaces (Window Detach Mode — ADR-028).
/// Esta ventana contiene SOLO el documento completo; NO incluye menú lateral ERP,
/// navegación principal, tabs Workspace, tabs módulo, ni shell completo.
/// La ventana ES el workspace documento únicamente.
/// </summary>
/// <remarks>
/// Gestionada exclusivamente por <see cref="Services.Windowing.IWindowManager"/> con
/// key prefix "detached:" para enforcement de límite máximo 2 ventanas simultáneas.
/// Implementada sin XAML para evitar complejidad source generator.
/// </remarks>
public sealed class DetachedDocumentWindow : Window
{
    /// <summary>
    /// Crea una ventana desacoplada con la página del documento especificada.
    /// </summary>
    /// <param name="documentPage">Página del documento (ej: CotizacionDocumentoPage) con su ViewModel completo.</param>
    /// <param name="title">Título de la ventana (ej: "Cotización - Cliente ABC").</param>
    public DetachedDocumentWindow(Page documentPage, string title)
    {
        Title = title;

        // Construir UI programáticamente (sin XAML)
        var grid = new Grid
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["ApplicationPageBackgroundThemeBrush"]
        };

        var contentPresenter = new ContentPresenter
        {
            Content = documentPage
        };

        grid.Children.Add(contentPresenter);
        Content = grid;

        // Configurar title bar estándar para evitar complejidad drag-region
        ExtendsContentIntoTitleBar = false;
    }
}

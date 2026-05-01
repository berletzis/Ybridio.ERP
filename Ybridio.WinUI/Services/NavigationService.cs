using Microsoft.UI.Xaml.Controls;

namespace Ybridio.WinUI.Services;

/// <summary>
/// Abstracción de navegación entre Pages usando Type en lugar de strings.
/// </summary>
public interface INavigationService
{
    /// <summary>Frame raíz donde se alojan las páginas.</summary>
    Frame? Frame { get; set; }

    /// <summary>Navega a la página indicada por tipo.</summary>
    bool NavigateTo(Type pageType, object? parameter = null);

    /// <summary>Retrocede si hay historial.</summary>
    bool GoBack();

    bool CanGoBack { get; }
}

/// <summary>
/// Implementación de <see cref="INavigationService"/> basada en <see cref="Frame"/>.
/// </summary>
public sealed class NavigationService : INavigationService
{
    public Frame? Frame { get; set; }

    public bool CanGoBack => Frame?.CanGoBack ?? false;

    public bool NavigateTo(Type pageType, object? parameter = null)
    {
        if (Frame is null)
            return false;

        // Evita re-navegación a la misma página con el mismo parámetro
        if (Frame.CurrentSourcePageType == pageType)
            return false;

        return Frame.Navigate(pageType, parameter);
    }

    public bool GoBack()
    {
        if (!CanGoBack)
            return false;

        Frame!.GoBack();
        return true;
    }
}

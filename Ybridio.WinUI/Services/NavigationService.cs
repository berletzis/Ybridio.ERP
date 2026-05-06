using System; // 🔥 ESTE FALTABA
using Microsoft.UI.Xaml.Controls;

namespace Ybridio.WinUI.Services;

public interface INavigationService
{
    Frame? Frame { get; set; }

    bool NavigateTo(Type pageType, object? parameter = null);

    bool GoBack();

    bool CanGoBack { get; }
}

public sealed class NavigationService : INavigationService
{
    public Frame? Frame { get; set; }

    public bool CanGoBack => Frame?.CanGoBack ?? false;

    public bool NavigateTo(Type pageType, object? parameter = null)
    {
        if (Frame is null)
            return false;

        // Permite re-navegación a la misma página cuando se pasa parámetro distinto
        // (p.ej. ConfiguracionPage con "Global" vs "Tienda")
        if (Frame.CurrentSourcePageType == pageType && parameter is null)
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
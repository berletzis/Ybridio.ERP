using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Views;

namespace Ybridio.WinUI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Registrar el Frame en el NavigationService
        var nav = App.Services.GetRequiredService<INavigationService>();
        nav.Frame = RootFrame;

        // Navegar al Login como primera pantalla
        nav.NavigateTo(typeof(LoginPage));
    }
}
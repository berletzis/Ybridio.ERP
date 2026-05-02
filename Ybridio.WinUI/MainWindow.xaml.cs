using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Views;

namespace Ybridio.WinUI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Extiende contenido a la title bar y la elimina visualmente
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(null);

        // Tamaño inicial
        AppWindow.Resize(new SizeInt32(1280, 800));

        // Registrar el Frame en el NavigationService
        var nav = App.Services.GetRequiredService<INavigationService>();
        nav.Frame = RootFrame;

        // Navegar al Login como primera pantalla
        nav.NavigateTo(typeof(LoginPage));
    }
}
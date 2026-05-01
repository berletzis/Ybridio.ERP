using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Views;

namespace Ybridio.WinUI;

/// <summary>
/// Ventana principal. Su única responsabilidad es conectar el Frame raíz
/// al NavigationService y realizar la navegación inicial hacia LoginPage.
/// Toda la lógica de negocio vive en los ViewModels.
/// </summary>
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
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
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

        // Extender contenido hasta el borde superior (elimina barra nativa)
        ExtendsContentIntoTitleBar = true;

        // Altura "Tall" (48px lógicos): coincide con ShellPage Row 0 Height="48"
        // y garantiza que los caption buttons queden dentro de esa franja
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        // Caption buttons con fondo transparente para integrarse con el contenido
        AppWindow.TitleBar.ButtonBackgroundColor         = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        // Actualizar drag region ante cualquier cambio de tamaño o modo
        AppWindow.Changed += (_, args) =>
        {
            if (args.DidSizeChange || args.DidPresenterChange)
                UpdateDragRegion();
        };

        AppWindow.Resize(new SizeInt32(1280, 800));
        UpdateDragRegion(); // llamada inicial con el tamaño ya establecido

        var nav = App.Services.GetRequiredService<INavigationService>();
        nav.Frame = RootFrame;
        nav.NavigateTo(typeof(LoginPage));
    }

    /// <summary>
    /// Define la región de drag de la title bar usando AppWindowTitleBar.SetDragRectangles.
    /// Excluye el área de caption buttons (RightInset) y el área del sistema izquierda (LeftInset).
    /// Se llama automáticamente en cada resize y cambio de estado.
    /// </summary>
    internal void UpdateDragRegion()
    {
        var titleBar   = AppWindow.TitleBar;
        var winWidth   = AppWindow.Size.Width;
        var dragHeight = titleBar.Height;      // físicos: altura del área de title bar (Tall = 48*DPI)
        var leftInset  = titleBar.LeftInset;   // físicos: área reservada izquierda (ícono/menú sistema)
        var rightInset = titleBar.RightInset;  // físicos: área reservada derecha (min/max/close)

        var dragWidth = winWidth - leftInset - rightInset;
        if (dragWidth <= 0 || dragHeight <= 0) return;

        // Drag region = barra completa MENOS los botones del sistema
        titleBar.SetDragRectangles([new RectInt32(leftInset, 0, dragWidth, dragHeight)]);
    }

    /// <summary>
    /// Ancho en píxeles físicos del área reservada para los caption buttons (min/max/close).
    /// ShellPage lo usa para ajustar el padding derecho del top bar y evitar superposición.
    /// </summary>
    internal int TitleBarRightInset => AppWindow.TitleBar.RightInset;
}

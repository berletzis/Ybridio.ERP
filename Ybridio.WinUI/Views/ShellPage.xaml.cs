using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Core;
using Ybridio.WinUI.ViewModels;
using XamlApp = Microsoft.UI.Xaml.Application;

namespace Ybridio.WinUI.Views;

public sealed partial class ShellPage : Page
{
    public ShellViewModel ViewModel { get; }

    public ShellPage()
    {
        ViewModel = App.Services.GetRequiredService<ShellViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeAsync(InnerFrame);
        SetActiveNavButton(BtnDashboard);
        AjustarPaddingTopBar();
    }

    /// <summary>
    /// Ajusta el padding derecho del top bar para que el contenido no invada
    /// el área de los caption buttons (minimizar/maximizar/cerrar).
    /// Convierte el RightInset de píxeles físicos a lógicos usando RasterizationScale.
    /// </summary>
    private void AjustarPaddingTopBar()
    {
        var mainWindow = App.Services.GetRequiredService<MainWindow>();
        var rightInsetFisico = mainWindow.TitleBarRightInset;
        var scale = XamlRoot?.RasterizationScale ?? 1.0;

        // Convertir físicos → lógicos; usar 0 si RightInset aún no está disponible
        var rightPadding = rightInsetFisico > 0 ? (int)(rightInsetFisico / scale) : 0;

        // Padding: left=12, top=0, right=zona_botones_sistema, bottom=0
        TopBarGrid.Padding = new Microsoft.UI.Xaml.Thickness(12, 0, rightPadding, 0);
    }

    private void ModuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            ViewModel.SelectModuleCommand.Execute(tag);
            SetActiveNavButton(btn);
        }
    }

    private void TiendaSelector_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SucursalDto tienda)
        {
            ViewModel.SeleccionarSucursalCommand.Execute(tienda);
            SucursalFlyout.Hide();
        }
    }

    private void RibbonButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
            ViewModel.NavigateToCommand.Execute(tag);
    }

    private void SetActiveNavButton(Button activeBtn)
    {
        foreach (UIElement child in NavButtonsPanel.Children)
        {
            if (child is Button btn)
                btn.ClearValue(BackgroundProperty);
        }
        if (XamlApp.Current.Resources.ContainsKey("SubtleFillColorSecondaryBrush"))
            activeBtn.Background = (Brush)XamlApp.Current.Resources["SubtleFillColorSecondaryBrush"];
    }
}

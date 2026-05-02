using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.Views;

public sealed partial class ShellPage : Page
{
    public ShellViewModel ViewModel { get; }

    public ShellPage()
    {
        // ViewModel debe asignarse ANTES de InitializeComponent para que
        // los bindings compilados x:Bind lo vean desde el inicio y no
        // usen Visibility.Visible como valor por defecto (null path).
        ViewModel = App.Services.GetRequiredService<ShellViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // Conectar el Frame interno y navegar al Dashboard
        await ViewModel.InitializeAsync(InnerFrame);
    }

    private void ModuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
            ViewModel.SelectModuleCommand.Execute(tag);
    }

    private void RibbonButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
            ViewModel.NavigateToCommand.Execute(tag);
    }
}

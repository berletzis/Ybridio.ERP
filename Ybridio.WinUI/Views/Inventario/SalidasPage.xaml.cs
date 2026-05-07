using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels.Inventario;

namespace Ybridio.WinUI.Views.Inventario;

public sealed partial class SalidasPage : Page, ILiveContextReporter
{
    public SalidasViewModel ViewModel { get; }

    public SalidasPage()
    {
        ViewModel = App.Services.GetRequiredService<SalidasViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.RefrescarAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.DetachFromContext();
    }

    /// <inheritdoc/>
    public void ReportLiveContext() => ViewModel.ReportLiveContext();
}

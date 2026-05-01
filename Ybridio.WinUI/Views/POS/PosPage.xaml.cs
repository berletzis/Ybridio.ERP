using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.WinUI.ViewModels.POS;

namespace Ybridio.WinUI.Views.POS;

public sealed partial class PosPage : Page
{
    public PosViewModel ViewModel { get; }

    public PosPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<PosViewModel>();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadProductosCommand.ExecuteAsync(null);
    }
}
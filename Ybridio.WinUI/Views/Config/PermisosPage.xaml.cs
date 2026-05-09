using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.WinUI.ViewModels.Config;

namespace Ybridio.WinUI.Views.Config;

public sealed partial class PermisosPage : Page
{
    public PermisosViewModel ViewModel { get; }

    public PermisosPage()
    {
        ViewModel = App.Services.GetRequiredService<PermisosViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.LoadCommand.CanExecute(null))
            await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.DetachFromContext();
    }
}

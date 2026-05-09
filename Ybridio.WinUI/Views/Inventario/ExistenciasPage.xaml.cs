using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.WinUI.ViewModels.Inventario;

namespace Ybridio.WinUI.Views.Inventario;

/// <summary>
/// Muestra las existencias de inventario del usuario según sus scopes de almacén autorizados.
/// El servicio aplica enforcement de autorización (existencia.ver) y scope de almacén antes de retornar datos.
/// </summary>
public sealed partial class ExistenciasPage : Page
{
    public ExistenciasViewModel ViewModel { get; }

    public ExistenciasPage()
    {
        ViewModel = App.Services.GetRequiredService<ExistenciasViewModel>();
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

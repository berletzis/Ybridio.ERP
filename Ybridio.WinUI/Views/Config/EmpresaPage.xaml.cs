using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.WinUI.ViewModels.Config;

namespace Ybridio.WinUI.Views.Config;

/// <summary>
/// Pantalla de Empresa — implementa el Singleton Operational Surface Pattern.
/// Grid institucional (izquierda) + Surface de edición (derecha).
/// Toda la lógica de negocio vive en EmpresaViewModel; este code-behind
/// solo orquesta ciclo de vida de la Page.
/// </summary>
public sealed partial class EmpresaPage : Page
{
    public EmpresaViewModel ViewModel { get; }

    public EmpresaPage()
    {
        ViewModel = App.Services.GetRequiredService<EmpresaViewModel>();
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

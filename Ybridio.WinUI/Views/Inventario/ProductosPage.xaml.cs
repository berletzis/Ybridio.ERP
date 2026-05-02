using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System.Linq;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.WinUI.ViewModels.Inventario;

namespace Ybridio.WinUI.Views.Inventario;

public sealed partial class ProductosPage : Page
{
    public ProductosViewModel ViewModel { get; }

    public ProductosPage()
    {
        ViewModel = App.Services.GetRequiredService<ProductosViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        ViewModel.SolicitarAbrirDetalle = AbrirVentanaDetalle;
        ViewModel.SolicitarComparar = AbrirVentanaComparar;

        if (ViewModel.LoadCommand.CanExecute(null))
            await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private void ListaProductos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.ProductosSeleccionados.Clear();
        foreach (var item in ListaProductos.SelectedItems.OfType<ProductoDto>())
            ViewModel.ProductosSeleccionados.Add(item);

        ViewModel.ProductoSeleccionado = ListaProductos.SelectedItem as ProductoDto;
        ViewModel.CompararCommand.NotifyCanExecuteChanged();
    }

    private void ListaProductos_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.ProductoSeleccionado is not null && ViewModel.EditarCommand.CanExecute(null))
            ViewModel.EditarCommand.Execute(null);
    }

    private void AbrirVentanaDetalle(ProductoDto? producto)
    {
        var ventana = new ProductoDetailWindow(ViewModel, producto);
        ventana.Activate();
    }

    private void AbrirVentanaComparar((ProductoDto A, ProductoDto B) par)
    {
        var ventana = new CompararProductosWindow(par.A, par.B);
        ventana.Activate();
    }
}

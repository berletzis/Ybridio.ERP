using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.WinUI.ViewModels.Config;

namespace Ybridio.WinUI.Views.Config;

public sealed partial class SucursalesConfigPage : Page
{
    public SucursalesConfigViewModel ViewModel { get; }

    public SucursalesConfigPage()
    {
        ViewModel = App.Services.GetRequiredService<SucursalesConfigViewModel>();
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

    private async void BtnNueva_Click(object sender, RoutedEventArgs e)
    {
        var txt = new TextBox { Header = "Nombre *", PlaceholderText = "Nombre de la sucursal" };
        var dialog = new ContentDialog
        {
            Title             = "Nueva sucursal",
            PrimaryButtonText = "Crear",
            CloseButtonText   = "Cancelar",
            DefaultButton     = ContentDialogButton.Primary,
            Content           = txt,
            XamlRoot          = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(txt.Text))
            await ViewModel.CrearAsync(txt.Text);
    }

    private async void BtnEditar_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SucursalSeleccionada is null) return;
        var txt = new TextBox
        {
            Header = "Nombre *",
            Text   = ViewModel.SucursalSeleccionada.Nombre
        };
        var dialog = new ContentDialog
        {
            Title             = "Editar sucursal",
            PrimaryButtonText = "Guardar",
            CloseButtonText   = "Cancelar",
            DefaultButton     = ContentDialogButton.Primary,
            Content           = txt,
            XamlRoot          = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(txt.Text))
            await ViewModel.ActualizarAsync(ViewModel.SucursalSeleccionada.Id, txt.Text);
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        => BtnEditar_Click(sender, new RoutedEventArgs());
}

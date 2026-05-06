using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.WinUI.ViewModels.Config;

namespace Ybridio.WinUI.Views.Config;

public sealed partial class RolesPage : Page
{
    public RolesViewModel ViewModel { get; }

    public RolesPage()
    {
        ViewModel = App.Services.GetRequiredService<RolesViewModel>();
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

    private async void BtnNuevo_Click(object sender, RoutedEventArgs e)
    {
        var txt = new TextBox { Header = "Nombre del rol *", PlaceholderText = "Ej: Administrador" };
        var dialog = new ContentDialog
        {
            Title             = "Nuevo rol",
            PrimaryButtonText = "Crear",
            CloseButtonText   = "Cancelar",
            DefaultButton     = ContentDialogButton.Primary,
            Content           = txt,
            XamlRoot          = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(txt.Text))
        {
            var result = await ViewModel.Service.CrearAsync(txt.Text);
            if (result.Success && result.Value is not null)
            {
                ViewModel.Roles.Add(result.Value);
                ViewModel.SuccessMessage = $"Rol '{result.Value.Name}' creado.";
            }
            else { ViewModel.ErrorMessage = result.Error ?? "No se pudo crear el rol."; }
        }
    }
}

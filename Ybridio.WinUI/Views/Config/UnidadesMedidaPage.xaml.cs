using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.WinUI.ViewModels.Config;

namespace Ybridio.WinUI.Views.Config;

public sealed partial class UnidadesMedidaPage : Page
{
    public UnidadesMedidaViewModel ViewModel { get; }

    public UnidadesMedidaPage()
    {
        ViewModel = App.Services.GetRequiredService<UnidadesMedidaViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.SolicitarNuevoEditar = AbrirDialogoNuevoEditar;
        if (ViewModel.LoadCommand.CanExecute(null))
            await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.DetachFromContext();
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.UnidadSeleccionada is not null) ViewModel.EditarCommand.Execute(null);
    }

    private async void AbrirDialogoNuevoEditar(UnidadMedidaDto? unidad)
    {
        var txtNombre       = new TextBox { PlaceholderText = "ej: Kilogramo", Text = unidad?.Nombre ?? string.Empty };
        var txtAbreviatura  = new TextBox { PlaceholderText = "ej: Kg", Text = unidad?.Abreviatura ?? string.Empty, MaxLength = 20 };
        var chkActivo       = new CheckBox { Content = "Activo", IsChecked = unidad?.Activo ?? true };

        var panel = new StackPanel { Spacing = 10, MinWidth = 300 };
        panel.Children.Add(new TextBlock { Text = "Nombre *", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(txtNombre);
        panel.Children.Add(new TextBlock { Text = "Abreviatura *", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(txtAbreviatura);
        if (unidad is not null) panel.Children.Add(chkActivo);

        var dialog = new ContentDialog
        {
            Title               = unidad is null ? "Nueva Unidad de Medida" : "Editar Unidad de Medida",
            PrimaryButtonText   = "Guardar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = panel
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        await ViewModel.GuardarUnidadAsync(
            unidad, txtNombre.Text.Trim(), txtAbreviatura.Text.Trim(), chkActivo.IsChecked ?? true);
    }
}

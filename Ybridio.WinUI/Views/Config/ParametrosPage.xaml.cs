using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.WinUI.ViewModels.Config;

namespace Ybridio.WinUI.Views.Config;

/// <summary>
/// Página CRUD para Parámetros de Configuración Global.
/// Los parámetros representan la configuración operacional de la empresa (IVA default, moneda, vigencias, etc.).
/// </summary>
public sealed partial class ParametrosPage : Page
{
    public ParametrosViewModel ViewModel { get; }

    public ParametrosPage()
    {
        ViewModel = App.Services.GetRequiredService<ParametrosViewModel>();
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
        if (ViewModel.ParametroSeleccionado is not null) ViewModel.EditarCommand.Execute(null);
    }

    private async void AbrirDialogoNuevoEditar(ParametroGlobalDto? param)
    {
        var txtClave       = new TextBox { PlaceholderText = "ej: iva.tasa.default", Text = param?.Clave ?? string.Empty, IsReadOnly = param is not null };
        var txtValor       = new TextBox { PlaceholderText = "Valor", Text = param?.Valor ?? string.Empty };
        var txtDescripcion = new TextBox { PlaceholderText = "Descripción (opcional)", Text = param?.Descripcion ?? string.Empty };
        var txtGrupo       = new TextBox { PlaceholderText = "ej: Fiscal, Moneda, Documentos", Text = param?.Grupo ?? "General" };
        var nbOrden        = new NumberBox { Value = param?.OrdenVisual ?? 0, Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var chkActivo      = new CheckBox { Content = "Activo", IsChecked = param?.Activo ?? true };

        var cmbTipoDato = new ComboBox { PlaceholderText = "Tipo de dato" };
        foreach (var t in new[] { "string", "decimal", "int", "bool" }) cmbTipoDato.Items.Add(t);
        cmbTipoDato.SelectedItem = param?.TipoDato ?? "string";

        var panel = new StackPanel { Spacing = 10, MinWidth = 380 };
        panel.Children.Add(new TextBlock { Text = "Clave *", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(txtClave);
        if (param is not null)
            panel.Children.Add(new TextBlock { Text = "(La clave no se puede modificar)", FontSize = 11,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"] });
        panel.Children.Add(new TextBlock { Text = "Valor *", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(txtValor);
        panel.Children.Add(new TextBlock { Text = "Tipo de dato", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(cmbTipoDato);
        panel.Children.Add(new TextBlock { Text = "Grupo", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(txtGrupo);
        panel.Children.Add(new TextBlock { Text = "Descripción", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(txtDescripcion);
        panel.Children.Add(new TextBlock { Text = "Orden Visual", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(nbOrden);
        if (param is not null) panel.Children.Add(chkActivo);

        var dialog = new ContentDialog
        {
            Title               = param is null ? "Nuevo Parámetro" : "Editar Parámetro",
            PrimaryButtonText   = "Guardar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = new ScrollViewer { Content = panel, MaxHeight = 520, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        await ViewModel.GuardarParametroAsync(
            param,
            txtClave.Text.Trim(),
            txtValor.Text.Trim(),
            string.IsNullOrWhiteSpace(txtDescripcion.Text) ? null : txtDescripcion.Text.Trim(),
            cmbTipoDato.SelectedItem?.ToString() ?? "string",
            string.IsNullOrWhiteSpace(txtGrupo.Text) ? "General" : txtGrupo.Text.Trim(),
            (int)(nbOrden.Value),
            chkActivo.IsChecked ?? true);
    }
}

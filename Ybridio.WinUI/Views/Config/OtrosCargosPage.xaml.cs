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
/// Página CRUD para Otros Cargos (Flete, Maniobras, Seguro, etc.).
/// Cargos accesorios documentales — NO son productos inventariables.
/// </summary>
public sealed partial class OtrosCargosPage : Page
{
    public OtrosCargosViewModel ViewModel { get; }

    public OtrosCargosPage()
    {
        ViewModel = App.Services.GetRequiredService<OtrosCargosViewModel>();
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
        if (ViewModel.CargoSeleccionado is not null) ViewModel.EditarCommand.Execute(null);
    }

    private async void AbrirDialogoNuevoEditar(OtroCargoDto? cargo)
    {
        var txtCodigo  = new TextBox { PlaceholderText = "ej: FLT", Text = cargo?.Codigo ?? string.Empty, MaxLength = 20 };
        var txtNombre  = new TextBox { PlaceholderText = "ej: Flete Nacional", Text = cargo?.Nombre ?? string.Empty };

        var cmbTipo = new ComboBox { PlaceholderText = "Tipo de cargo" };
        foreach (var t in OtrosCargosViewModel.TiposCargo) cmbTipo.Items.Add(t);
        cmbTipo.SelectedItem = cargo?.TipoCargo ?? "Otro";

        var cmbImpuesto = new ComboBox { PlaceholderText = "Sin impuesto específico" };
        cmbImpuesto.Items.Add("(Sin impuesto)");
        foreach (var imp in ViewModel.TiposImpuesto) cmbImpuesto.Items.Add(imp);
        cmbImpuesto.SelectedIndex = 0;
        if (cargo?.TipoImpuestoId is int tiId)
        {
            var match = ViewModel.TiposImpuesto.FirstOrDefault(i => i.Id == tiId);
            if (match is not null) cmbImpuesto.SelectedItem = match;
        }

        var chkAplicaIva = new CheckBox { Content = "Aplica IVA", IsChecked = cargo?.AplicaIva ?? false };
        var nbOrden      = new NumberBox { Value = cargo?.OrdenVisual ?? 0, Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var chkActivo    = new CheckBox { Content = "Activo", IsChecked = cargo?.Activo ?? true };

        var panel = new StackPanel { Spacing = 10, MinWidth = 360 };
        panel.Children.Add(new TextBlock { Text = "Código *", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(txtCodigo);
        panel.Children.Add(new TextBlock { Text = "Nombre *", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(txtNombre);
        panel.Children.Add(new TextBlock { Text = "Tipo de Cargo", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(cmbTipo);
        panel.Children.Add(chkAplicaIva);
        panel.Children.Add(new TextBlock { Text = "Tipo de Impuesto (opcional)", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(cmbImpuesto);
        panel.Children.Add(new TextBlock { Text = "Orden Visual", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(nbOrden);
        if (cargo is not null) panel.Children.Add(chkActivo);

        var dialog = new ContentDialog
        {
            Title               = cargo is null ? "Nuevo Cargo" : "Editar Cargo",
            PrimaryButtonText   = "Guardar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = new ScrollViewer { Content = panel, MaxHeight = 520, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        int? tipoImpId = null;
        if (cmbImpuesto.SelectedItem is TipoImpuestoDto selImp)
            tipoImpId = selImp.Id;

        await ViewModel.GuardarCargoAsync(
            cargo,
            txtCodigo.Text.Trim(),
            txtNombre.Text.Trim(),
            cmbTipo.SelectedItem?.ToString() ?? "Otro",
            chkAplicaIva.IsChecked ?? false,
            tipoImpId,
            (int)nbOrden.Value,
            chkActivo.IsChecked ?? true);
    }
}

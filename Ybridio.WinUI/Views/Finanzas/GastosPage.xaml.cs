using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Finanzas;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels.Finanzas;
using XamlApp = Microsoft.UI.Xaml.Application;

namespace Ybridio.WinUI.Views.Finanzas;

public sealed partial class GastosPage : Page, ILiveContextReporter
{
    public GastosViewModel ViewModel { get; }

    public GastosPage()
    {
        ViewModel = App.Services.GetRequiredService<GastosViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.SolicitarNuevoEditar = AbrirDialogoNuevoEditar;
        if (ViewModel.RefrescarCommand.CanExecute(null))
            await ViewModel.RefrescarCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.DetachFromContext();
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.GastoSeleccionado is not null) ViewModel.EditarCommand.Execute(null);
    }

    public void ReportLiveContext() => ViewModel.ReportLiveContext();

    private async void AbrirDialogoNuevoEditar(MovimientoFinancieroDto? gasto)
    {
        var txtConcepto     = new TextBox { PlaceholderText = "Concepto del gasto",     Text = gasto?.Concepto ?? string.Empty };
        var txtMonto        = new TextBox { PlaceholderText = "Monto",                  Text = gasto?.Monto.ToString("F2") ?? string.Empty };
        var dpFecha         = new DatePicker { Date = gasto?.Fecha ?? DateTime.Today };
        var cbCategoria     = new ComboBox { PlaceholderText = "Categoría (opcional)", MinWidth = 240 };
        var txtObservaciones = new TextBox { PlaceholderText = "Observaciones (opcional)", Text = gasto?.Observaciones ?? string.Empty };

        // Llenar categorías
        foreach (var cat in ViewModel.Categorias.Where(c => c.TipoAplicable is "Gasto" or "Ambos"))
            cbCategoria.Items.Add(cat);
        cbCategoria.DisplayMemberPath = "Nombre";
        if (gasto?.CategoriaId.HasValue == true)
            cbCategoria.SelectedItem = ViewModel.Categorias.FirstOrDefault(c => c.Id == gasto.CategoriaId);

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = "Concepto *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(txtConcepto);
        panel.Children.Add(new TextBlock { Text = "Monto *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(txtMonto);
        panel.Children.Add(new TextBlock { Text = "Fecha", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(dpFecha);
        panel.Children.Add(new TextBlock { Text = "Categoría", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(cbCategoria);
        panel.Children.Add(new TextBlock { Text = "Observaciones", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(txtObservaciones);

        var dialog = new ContentDialog
        {
            Title               = gasto is null ? "Nuevo Gasto" : "Editar Gasto",
            PrimaryButtonText   = "Guardar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = new ScrollViewer { Content = panel, MaxHeight = 500, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        if (!decimal.TryParse(txtMonto.Text.Trim(), out var monto) || monto <= 0)
        {
            ViewModel.ErrorMessage = "El monto debe ser un número mayor a cero.";
            return;
        }

        var concepto    = txtConcepto.Text.Trim();
        if (string.IsNullOrEmpty(concepto)) { ViewModel.ErrorMessage = "El concepto es obligatorio."; return; }

        var categoriaId = (cbCategoria.SelectedItem as CategoriaFinancieraDto)?.Id;
        var obs         = string.IsNullOrWhiteSpace(txtObservaciones.Text) ? null : txtObservaciones.Text.Trim();
        var fecha       = dpFecha.Date.DateTime;

        await ViewModel.GuardarAsync(gasto, concepto, monto, fecha, categoriaId, obs);
    }
}


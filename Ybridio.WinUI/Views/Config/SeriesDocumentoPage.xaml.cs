using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.Domain.Catalogos;
using Ybridio.WinUI.ViewModels.Config;

namespace Ybridio.WinUI.Views.Config;

/// <summary>
/// Página CRUD para Series Documentales — configura prefijos, longitudes y consecutivos de folios.
/// La generación runtime de folios es responsabilidad de IFolioGeneratorService (Application layer).
/// </summary>
public sealed partial class SeriesDocumentoPage : Page
{
    public SeriesDocumentoViewModel ViewModel { get; }

    public SeriesDocumentoPage()
    {
        ViewModel = App.Services.GetRequiredService<SeriesDocumentoViewModel>();
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
        if (ViewModel.SerieSeleccionada is not null) ViewModel.EditarCommand.Execute(null);
    }

    private async void AbrirDialogoNuevoEditar(SerieDocumentoDto? serie)
    {
        // ── Tipo documento ─────────────────────────────────────────────────────
        var cmbTipo = new ComboBox { PlaceholderText = "Selecciona el tipo de documento", MinWidth = 300 };
        foreach (var (tipo, nombre) in SeriesDocumentoViewModel.TiposDocumento)
        {
            var item = new ComboBoxItem { Content = nombre, Tag = tipo };
            cmbTipo.Items.Add(item);
            if (serie is not null && tipo == serie.TipoDocumento)
                cmbTipo.SelectedItem = item;
        }
        if (serie is null) cmbTipo.SelectedIndex = 0;

        var isEdit = serie is not null;
        if (isEdit)
        {
            // En edición el tipo no puede cambiarse (integridad documental)
            cmbTipo.IsEnabled = false;
        }

        // ── Prefijo ────────────────────────────────────────────────────────────
        var txtPrefijo = new TextBox
        {
            PlaceholderText = "ej: COT, PED, VTA",
            Text            = serie?.Prefijo ?? string.Empty,
            MaxLength       = 20,
        };

        // ── Longitud ───────────────────────────────────────────────────────────
        var nbLongitud = new NumberBox
        {
            Value                    = serie?.Longitud ?? 6,
            Minimum                  = 1,
            Maximum                  = 12,
            SpinButtonPlacementMode  = NumberBoxSpinButtonPlacementMode.Compact,
            SmallChange              = 1,
        };

        // ── Siguiente número ───────────────────────────────────────────────────
        var nbSiguiente = new NumberBox
        {
            Value                   = serie?.SiguienteNumero ?? 1,
            Minimum                 = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        };

        var chkReinicio = new CheckBox { Content = "Reinicio anual (preparado para V2)", IsChecked = serie?.ReinicioAnual ?? false, IsEnabled = false };
        var chkActivo   = new CheckBox { Content = "Activo", IsChecked = serie?.Activo ?? true };

        // Vista previa del folio
        var txtPreview = new TextBlock
        {
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code, Consolas, monospace"),
            FontSize   = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentFillColorDefaultBrush"]
        };

        void ActualizarPreview()
        {
            var pref = txtPrefijo.Text.Trim().ToUpperInvariant();
            var num  = (long)(double.IsNaN(nbSiguiente.Value) ? 1 : nbSiguiente.Value);
            var lon  = (int)(double.IsNaN(nbLongitud.Value) ? 6 : nbLongitud.Value);
            txtPreview.Text = string.IsNullOrWhiteSpace(pref)
                ? "—"
                : $"{pref}-{num.ToString().PadLeft(lon, '0')}";
        }

        txtPrefijo.TextChanged     += (_, _) => ActualizarPreview();
        nbLongitud.ValueChanged    += (_, _) => ActualizarPreview();
        nbSiguiente.ValueChanged   += (_, _) => ActualizarPreview();
        ActualizarPreview();

        var panel = new StackPanel { Spacing = 12, MinWidth = 380 };
        panel.Children.Add(new TextBlock { Text = "Tipo de Documento", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(cmbTipo);
        if (isEdit)
            panel.Children.Add(new TextBlock
            {
                Text      = "El tipo de documento no puede modificarse en una serie existente.",
                FontSize  = 11,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"]
            });

        panel.Children.Add(new TextBlock { Text = "Prefijo *", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(txtPrefijo);
        panel.Children.Add(new TextBlock { Text = "Longitud (dígitos)", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(nbLongitud);
        panel.Children.Add(new TextBlock { Text = "Siguiente Número", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(nbSiguiente);
        panel.Children.Add(chkReinicio);
        if (isEdit) panel.Children.Add(chkActivo);

        panel.Children.Add(new Border
        {
            Height     = 1,
            Background = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["DividerStrokeColorDefaultBrush"],
            Margin     = new Thickness(0, 4, 0, 4)
        });
        panel.Children.Add(new TextBlock { Text = "Vista previa del próximo folio:", FontSize = 11,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"] });
        panel.Children.Add(txtPreview);

        var dialog = new ContentDialog
        {
            Title               = serie is null ? "Nueva Serie Documental" : "Editar Serie Documental",
            PrimaryButtonText   = "Guardar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = new ScrollViewer { Content = panel, MaxHeight = 560, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        // Obtener tipo seleccionado
        var tipoSeleccionado = cmbTipo.SelectedItem is ComboBoxItem ci && ci.Tag is TipoDocumentoSerie t
            ? t
            : (serie?.TipoDocumento ?? TipoDocumentoSerie.Cotizacion);

        await ViewModel.GuardarSerieAsync(
            serie,
            tipoSeleccionado,
            txtPrefijo.Text.Trim(),
            (int)(double.IsNaN(nbLongitud.Value) ? 6 : nbLongitud.Value),
            (long)(double.IsNaN(nbSiguiente.Value) ? 1 : nbSiguiente.Value),
            chkReinicio.IsChecked ?? false,
            chkActivo.IsChecked ?? true);
    }
}

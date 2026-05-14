using System;
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
/// Catálogo Fiscal Institucional — Commercial Tax Pattern.
/// Define los tipos de impuesto (IVA 16%, IVA 8%, Exento, IEPS, etc.) disponibles por empresa.
/// Es la ÚNICA fuente de verdad fiscal. Los ParametroGlobal fiscales referencian el Id de este catálogo.
/// </summary>
public sealed partial class ImpuestosPage : Page
{
    public ImpuestosViewModel ViewModel { get; }

    public ImpuestosPage()
    {
        ViewModel = App.Services.GetRequiredService<ImpuestosViewModel>();
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
        if (ViewModel.ImpuestoSeleccionado is not null) ViewModel.EditarCommand.Execute(null);
    }

    private async void AbrirDialogoNuevoEditar(TipoImpuestoDto? impuesto)
    {
        // ── Nombre ────────────────────────────────────────────────────────────
        var txtNombre = new TextBox
        {
            PlaceholderText = "ej: IVA 16%, IVA Frontera 8%",
            Text            = impuesto?.Nombre ?? string.Empty
        };

        // ── Código ────────────────────────────────────────────────────────────
        var txtCodigo = new TextBox
        {
            PlaceholderText = "ej: IVA16, IVA8, EXENTO",
            Text            = impuesto?.Codigo ?? string.Empty,
            MaxLength       = 20
        };

        // ── Tipo Gravamen ──────────────────────────────────────────────────────
        var cmbGravamen = new ComboBox { PlaceholderText = "Tipo de gravamen" };
        cmbGravamen.Items.Add(new ComboBoxItem { Content = "IVA",            Tag = TipoGravamen.IVA });
        cmbGravamen.Items.Add(new ComboBoxItem { Content = "IEPS",           Tag = TipoGravamen.IEPS });
        cmbGravamen.Items.Add(new ComboBoxItem { Content = "ISR Retención",  Tag = TipoGravamen.ISRRetencion });
        cmbGravamen.Items.Add(new ComboBoxItem { Content = "Exento",         Tag = TipoGravamen.Exento });
        cmbGravamen.Items.Add(new ComboBoxItem { Content = "Otro",           Tag = TipoGravamen.Otro });

        var gravamenActual = impuesto?.Gravamen ?? TipoGravamen.IVA;
        foreach (ComboBoxItem item in cmbGravamen.Items)
            if (item.Tag is TipoGravamen g && g == gravamenActual)
            { cmbGravamen.SelectedItem = item; break; }
        if (cmbGravamen.SelectedItem is null) cmbGravamen.SelectedIndex = 0;

        // ── Porcentaje ────────────────────────────────────────────────────────
        var nbPorcentaje = new NumberBox
        {
            Value                   = (double)(impuesto?.Porcentaje ?? 16m),
            Minimum                 = 0,
            Maximum                 = 100,
            SmallChange             = 0.5,
            LargeChange             = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            NumberFormatter         = new Windows.Globalization.NumberFormatting.DecimalFormatter { FractionDigits = 2 }
        };

        // Cuando el tipo es Exento, bloquear porcentaje en 0
        cmbGravamen.SelectionChanged += (_, _) =>
        {
            if (cmbGravamen.SelectedItem is ComboBoxItem ci &&
                ci.Tag is TipoGravamen g && g == TipoGravamen.Exento)
            {
                nbPorcentaje.Value     = 0;
                nbPorcentaje.IsEnabled = false;
            }
            else
            {
                nbPorcentaje.IsEnabled = true;
            }
        };

        if (gravamenActual == TipoGravamen.Exento)
        {
            nbPorcentaje.Value     = 0;
            nbPorcentaje.IsEnabled = false;
        }

        // ── Orden visual ──────────────────────────────────────────────────────
        var nbOrden = new NumberBox
        {
            Value                   = impuesto?.OrdenVisual ?? 0,
            Minimum                 = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
        };

        // ── Descripción ───────────────────────────────────────────────────────
        var txtDescripcion = new TextBox
        {
            PlaceholderText = "Descripción técnica o legal (opcional)",
            Text            = impuesto?.Descripcion ?? string.Empty,
            TextWrapping    = TextWrapping.Wrap,
            AcceptsReturn   = false
        };

        // ── Activo (solo en edición) ───────────────────────────────────────────
        var chkActivo = new CheckBox { Content = "Activo", IsChecked = impuesto?.Activo ?? true };

        var panel = new StackPanel { Spacing = 10, MinWidth = 360 };
        panel.Children.Add(new TextBlock { Text = "Nombre *", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(txtNombre);
        panel.Children.Add(new TextBlock { Text = "Código *", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(txtCodigo);
        panel.Children.Add(new TextBlock
        {
            Text      = "El código se usa para integración programática (SAT, mapeo). Ej: IVA16",
            FontSize  = 11,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"]
        });
        panel.Children.Add(new TextBlock { Text = "Tipo de Gravamen *", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(cmbGravamen);
        panel.Children.Add(new TextBlock { Text = "Porcentaje (%)", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(nbPorcentaje);
        panel.Children.Add(new TextBlock { Text = "Orden Visual", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(nbOrden);
        panel.Children.Add(new TextBlock { Text = "Descripción", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(txtDescripcion);
        if (impuesto is not null) panel.Children.Add(chkActivo);

        var dialog = new ContentDialog
        {
            Title               = impuesto is null ? "Nuevo Tipo de Impuesto" : "Editar Tipo de Impuesto",
            PrimaryButtonText   = "Guardar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = new ScrollViewer
            {
                Content                      = panel,
                MaxHeight                    = 580,
                VerticalScrollBarVisibility  = ScrollBarVisibility.Auto
            }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var gravamenSeleccionado = cmbGravamen.SelectedItem is ComboBoxItem selItem && selItem.Tag is TipoGravamen t
            ? t
            : TipoGravamen.IVA;

        await ViewModel.GuardarImpuestoAsync(
            impuesto,
            txtNombre.Text.Trim(),
            (decimal)nbPorcentaje.Value,
            chkActivo.IsChecked ?? true,
            txtCodigo.Text.Trim(),
            gravamenSeleccionado,
            (int)nbOrden.Value,
            string.IsNullOrWhiteSpace(txtDescripcion.Text) ? null : txtDescripcion.Text.Trim());
    }
}

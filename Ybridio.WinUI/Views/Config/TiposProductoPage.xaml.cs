using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.WinUI.ViewModels.Config;

namespace Ybridio.WinUI.Views.Config;

/// <summary>
/// Catálogo de Tipos de Producto — Product Type Classification Pattern.
/// Los Servicios son Productos con TipoProducto.Clave="SERV" (no tabla separada).
/// La Clave es el identificador operacional humano para reglas de negocio.
/// </summary>
public sealed partial class TiposProductoPage : Page
{
    public TiposProductoViewModel ViewModel { get; }

    public TiposProductoPage()
    {
        ViewModel = App.Services.GetRequiredService<TiposProductoViewModel>();
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
        if (ViewModel.TipoSeleccionado is not null) ViewModel.EditarCommand.Execute(null);
    }

    private async void AbrirDialogoNuevoEditar(TipoProductoDto? tipo)
    {
        // ── Clave ──────────────────────────────────────────────────────────────
        var txtClave = new TextBox
        {
            PlaceholderText = "ej: PROD, SERV, REF, EQP",
            Text            = tipo?.Clave ?? string.Empty,
            MaxLength       = 10
        };

        // ── Nombre ─────────────────────────────────────────────────────────────
        var txtNombre = new TextBox
        {
            PlaceholderText = "ej: Producto Físico, Servicio, Refacción",
            Text            = tipo?.Nombre ?? string.Empty
        };

        // ── Descripción ────────────────────────────────────────────────────────
        var txtDescripcion = new TextBox
        {
            PlaceholderText = "Descripción (opcional)",
            Text            = tipo?.Descripcion ?? string.Empty
        };

        // ── Orden visual ───────────────────────────────────────────────────────
        var nbOrden = new NumberBox
        {
            Value                   = tipo?.OrdenVisual ?? 0,
            Minimum                 = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
        };

        var chkActivo = new CheckBox { Content = "Activo", IsChecked = tipo?.Activo ?? true };

        var panel = new StackPanel { Spacing = 10, MinWidth = 340 };
        panel.Children.Add(new TextBlock { Text = "Clave Operacional *", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(txtClave);
        panel.Children.Add(new TextBlock
        {
            Text      = "Código corto único que identifica el tipo (ej: PROD, SERV, REF). El operador lo usa en reglas de negocio.",
            FontSize  = 11,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"]
        });
        panel.Children.Add(new TextBlock { Text = "Nombre *", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(txtNombre);
        panel.Children.Add(new TextBlock { Text = "Descripción", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(txtDescripcion);
        panel.Children.Add(new TextBlock { Text = "Orden Visual", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(nbOrden);
        if (tipo is not null) panel.Children.Add(chkActivo);

        var dialog = new ContentDialog
        {
            Title               = tipo is null ? "Nuevo Tipo de Producto" : "Editar Tipo de Producto",
            PrimaryButtonText   = "Guardar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = new ScrollViewer
            {
                Content                     = panel,
                MaxHeight                   = 540,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        await ViewModel.GuardarTipoAsync(
            tipo,
            txtNombre.Text.Trim(),
            string.IsNullOrWhiteSpace(txtDescripcion.Text) ? null : txtDescripcion.Text.Trim(),
            chkActivo.IsChecked ?? true,
            txtClave.Text.Trim(),
            (int)nbOrden.Value);
    }
}

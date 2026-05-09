using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Finanzas;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels.Finanzas;

namespace Ybridio.WinUI.Views.Finanzas;

public sealed partial class CxCPage : Page, ILiveContextReporter
{
    public CxCViewModel ViewModel { get; }

    public CxCPage()
    {
        ViewModel = App.Services.GetRequiredService<CxCViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.SolicitarNuevo         = AbrirDialogoNueva;
        ViewModel.SolicitarRegistrarPago = AbrirDialogoPago;
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
        if (ViewModel.CxcSeleccionada is { SaldoPendiente: > 0 })
            ViewModel.RegistrarPagoCommand.Execute(null);
    }

    public void ReportLiveContext() => ViewModel.ReportLiveContext();

    private async void AbrirDialogoNueva(CxCDto? _)
    {
        var session = App.Services.GetRequiredService<Ybridio.WinUI.Services.SessionService>();

        var txtDeudor        = new TextBox { PlaceholderText = "Nombre del deudor" };
        var txtConcepto      = new TextBox { PlaceholderText = "Concepto (ej: Factura 045)" };
        var txtMonto         = new TextBox { PlaceholderText = "Monto original" };
        var dpEmision        = new DatePicker { Date = DateTime.Today };
        var dpVencimiento    = new DatePicker { Date = DateTime.Today.AddDays(30) };
        var txtObservaciones = new TextBox { PlaceholderText = "Observaciones (opcional)" };

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = "Deudor *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(txtDeudor);
        panel.Children.Add(new TextBlock { Text = "Concepto *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(txtConcepto);
        panel.Children.Add(new TextBlock { Text = "Monto Original *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(txtMonto);
        panel.Children.Add(new TextBlock { Text = "Fecha Emisión", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(dpEmision);
        panel.Children.Add(new TextBlock { Text = "Fecha Vencimiento", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(dpVencimiento);
        panel.Children.Add(new TextBlock { Text = "Observaciones", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(txtObservaciones);

        var dialog = new ContentDialog
        {
            Title = "Nueva Cuenta por Cobrar",
            PrimaryButtonText = "Guardar", SecondaryButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot,
            Content = new ScrollViewer { Content = panel, MaxHeight = 500, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        if (!decimal.TryParse(txtMonto.Text.Trim(), out var monto) || monto <= 0)
        { ViewModel.ErrorMessage = "El monto debe ser mayor a cero."; return; }

        var deudor   = txtDeudor.Text.Trim();
        var concepto = txtConcepto.Text.Trim();
        if (string.IsNullOrEmpty(deudor) || string.IsNullOrEmpty(concepto))
        { ViewModel.ErrorMessage = "Deudor y concepto son obligatorios."; return; }

        var dto = new CrearCxCDto(session.EmpresaId, session.SucursalId != 0 ? session.SucursalId : null,
            deudor, concepto, monto,
            dpEmision.Date.DateTime,
            dpVencimiento.Date.DateTime,
            string.IsNullOrWhiteSpace(txtObservaciones.Text) ? null : txtObservaciones.Text.Trim());

        await ViewModel.CrearCxCAsync(dto);
    }

    private async void AbrirDialogoPago(CxCDto cxc)
    {
        var txtMonto = new TextBox { PlaceholderText = $"Monto (saldo: {cxc.SaldoPendiente:F2})", Text = cxc.SaldoPendiente.ToString("F2") };

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = $"Deudor: {cxc.NombreDeudor}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = $"Saldo pendiente: {cxc.SaldoPendiente:C}", Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentFillColorDefaultBrush"] });
        panel.Children.Add(new TextBlock { Text = "Monto a pagar *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(txtMonto);

        var dialog = new ContentDialog
        {
            Title = "Registrar Pago",
            PrimaryButtonText = "Registrar", SecondaryButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot,
            Content = panel
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        if (!decimal.TryParse(txtMonto.Text.Trim(), out var monto) || monto <= 0)
        { ViewModel.ErrorMessage = "El monto debe ser mayor a cero."; return; }

        await ViewModel.PagarAsync(cxc.Id, monto);
    }
}


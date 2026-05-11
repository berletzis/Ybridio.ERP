using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.Services.Venta;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels.Ventas;

namespace Ybridio.WinUI.Views.Ventas;

/// <summary>
/// Host de Órdenes de Trabajo — sigue el Document Surface UX Pattern (ADR-025 + ADR-031).
/// Los documentos se abren inline reemplazando el listado, NO como tabs de workspace.
/// </summary>
public sealed partial class OrdenesTrabajoPage : Page, ILiveContextReporter
{
    private readonly IOrdenTrabajoService _otService;
    public OrdenesTrabajoViewModel ViewModel { get; }

    public OrdenesTrabajoPage()
    {
        ViewModel  = App.Services.GetRequiredService<OrdenesTrabajoViewModel>();
        _otService = App.Services.GetRequiredService<IOrdenTrabajoService>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.SolicitarAgregarMaterial = AbrirDialogoAgregarMaterialLegacy;
        if (ViewModel.RefrescarCommand.CanExecute(null))
            await ViewModel.RefrescarCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.DetachFromContext();
    }

    public void ReportLiveContext() => ViewModel.ReportLiveContext();

    private void BtnNuevaOT_Click(object sender, RoutedEventArgs e)
    {
        // ADR-031: nueva OT se abre inline como Document Surface, NO como workspace tab
        var page = new OrdenTrabajoDocumentoPage(null);
        page.OnCerrar = async () => await ViewModel.CerrarDocumentSurfaceAsync();
        ViewModel.AbrirDocumentoOT(page);
    }

    private async void BtnAbrirOT_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.OtSeleccionada is null) return;
        await AbrirOTInlineAsync(ViewModel.OtSeleccionada.Id);
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.OtSeleccionada is null) return;
        _ = AbrirOTInlineAsync(ViewModel.OtSeleccionada.Id);
    }

    private async System.Threading.Tasks.Task AbrirOTInlineAsync(long id)
    {
        ViewModel.IsBusy = true;
        ViewModel.ErrorMessage = string.Empty;
        try
        {
            var result = await _otService.ObtenerConMaterialesAsync(id);
            if (!result.Success)
            {
                ViewModel.ErrorMessage = result.Error ?? "Error al cargar la OT.";
                return;
            }
            // ADR-031: documento se carga como surface inline, SIN workspace tab
            var page = new OrdenTrabajoDocumentoPage(result.Value);
            page.OnCerrar = async () => await ViewModel.CerrarDocumentSurfaceAsync();
            ViewModel.AbrirDocumentoOT(page);
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }

    // Legacy inline dialog para "Agregar Material" desde el grid de lista
    private async void AbrirDialogoAgregarMaterialLegacy(Ybridio.Application.DTOs.Ventas.OTResumenDto ot)
    {
        var txtDesc   = new TextBox { PlaceholderText = "Material o servicio" };
        var txtQty    = new TextBox { PlaceholderText = "Cantidad", Text = "1" };
        var txtPrecio = new TextBox { PlaceholderText = "Precio unitario", Text = "0" };

        var panel = new StackPanel { Spacing = 10 };
        void Lbl(string t) => panel.Children.Add(new TextBlock { Text = t, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        Lbl("Descripcion *"); panel.Children.Add(txtDesc);
        Lbl("Cantidad *");    panel.Children.Add(txtQty);
        Lbl("Precio *");      panel.Children.Add(txtPrecio);

        var dialog = new ContentDialog
        {
            Title = $"Agregar Material - OT #{ot.Id}", PrimaryButtonText = "Agregar", SecondaryButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot, Content = panel
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var desc = txtDesc.Text.Trim();
        if (string.IsNullOrEmpty(desc)) { ViewModel.ErrorMessage = "Descripcion obligatoria."; return; }
        if (!decimal.TryParse(txtQty.Text, out var qty) || qty <= 0) { ViewModel.ErrorMessage = "Cantidad invalida."; return; }
        if (!decimal.TryParse(txtPrecio.Text, out var precio) || precio < 0) { ViewModel.ErrorMessage = "Precio invalido."; return; }

        var r = await ViewModel.AgregarMaterialAsync(ot.Id, new Ybridio.Application.DTOs.Ventas.AgregarOTMaterialDto(null, desc, qty, precio));
        if (r.Success) { ViewModel.SuccessMessage = "Material agregado."; await ViewModel.RefrescarCommand.ExecuteAsync(null); }
        else           { ViewModel.ErrorMessage   = r.Error ?? "No se pudo agregar."; }
    }
}

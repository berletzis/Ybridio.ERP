using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.Services.Venta;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.Services.Workspace;
using Ybridio.WinUI.ViewModels.Ventas;

namespace Ybridio.WinUI.Views.Ventas;

public sealed partial class OrdenesTrabajoPage : Page, ILiveContextReporter
{
    private readonly IWorkspaceService      _workspace;
    private readonly IOrdenTrabajoService   _otService;
    public OrdenesTrabajoViewModel ViewModel { get; }

    public OrdenesTrabajoPage()
    {
        ViewModel  = App.Services.GetRequiredService<OrdenesTrabajoViewModel>();
        _workspace = App.Services.GetRequiredService<IWorkspaceService>();
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

    // Abrir OT en WorkspaceService (nueva o existente)
    private async void BtnNuevaOT_Click(object sender, RoutedEventArgs e)
    {
        var session = App.Services.GetRequiredService<Ybridio.WinUI.Services.SessionService>();
        _workspace.OpenTab(
            key:         $"ot-nueva-{Guid.NewGuid():N}",
            title:       "Nueva OT",
            icon:        "",
            pageFactory: () => new OrdenTrabajoDocumentoPage(null),
            isClosable:  true);
    }

    private async void BtnAbrirOT_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.OtSeleccionada is null) return;
        await AbrirOTEnWorkspace(ViewModel.OtSeleccionada.Id);
    }

    private void Lista_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.OtSeleccionada is null) return;
        _ = AbrirOTEnWorkspace(ViewModel.OtSeleccionada.Id);
    }

    private async System.Threading.Tasks.Task AbrirOTEnWorkspace(long id)
    {
        var key = $"ot-{id}";
        if (_workspace.Exists(key)) { _workspace.ActivateTab(key); return; }

        var result = await _otService.ObtenerConMaterialesAsync(id);
        if (!result.Success) { ViewModel.ErrorMessage = result.Error ?? "Error al cargar OT."; return; }

        _workspace.OpenTab(
            key:         key,
            title:       $"OT #{id}",
            icon:        "",
            pageFactory: () => new OrdenTrabajoDocumentoPage(result.Value),
            isClosable:  true);
    }

    // Legacy inline dialog for "Agregar Material" desde el grid de lista (sigue funcionando)
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

        if (!decimal.TryParse(txtQty.Text, out var qty) || qty <= 0) { ViewModel.ErrorMessage = "Cantidad invalida."; return; }
        if (!decimal.TryParse(txtPrecio.Text, out var precio) || precio < 0) { ViewModel.ErrorMessage = "Precio invalido."; return; }
        var desc = txtDesc.Text.Trim();
        if (string.IsNullOrEmpty(desc)) { ViewModel.ErrorMessage = "Descripcion obligatoria."; return; }

        var r = await ViewModel.AgregarMaterialAsync(ot.Id, new Ybridio.Application.DTOs.Ventas.AgregarOTMaterialDto(null, desc, qty, precio));
        if (r.Success) { ViewModel.SuccessMessage = "Material agregado."; await ViewModel.RefrescarCommand.ExecuteAsync(null); }
        else           { ViewModel.ErrorMessage   = r.Error ?? "No se pudo agregar."; }
    }
}

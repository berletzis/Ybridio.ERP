using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Venta;
using Ybridio.Domain.Ventas;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.ViewModels.Ventas;

namespace Ybridio.WinUI.Views.Ventas;

/// <summary>Documento de OT persistente en WorkspaceService.</summary>
public sealed partial class OrdenTrabajoDocumentoPage : Page
{
    public OrdenTrabajoDocumentoViewModel ViewModel { get; }

    public OrdenTrabajoDocumentoPage(OrdenTrabajoDto? ot)
    {
        ViewModel = new OrdenTrabajoDocumentoViewModel(
            App.Services.GetRequiredService<IOrdenTrabajoService>(),
            App.Services.GetRequiredService<Ybridio.Application.Services.Autorizacion.IErpAuthorizationService>(),
            App.Services.GetRequiredService<SessionService>(),
            App.Services.GetRequiredService<Ybridio.WinUI.Services.Diagnostic.IOperationalObservabilityService>(),
            App.Services.GetRequiredService<Ybridio.WinUI.Services.Diagnostic.ICurrentContextTracker>());

        InitializeComponent();
        ViewModel.Initialize(ot);
    }

    private async void BtnAgregarMaterial_Click(object sender, RoutedEventArgs e)
    {
        var txtDesc   = new TextBox { PlaceholderText = "Material o servicio" };
        var txtQty    = new TextBox { PlaceholderText = "Cantidad", Text = "1" };
        var txtPrecio = new TextBox { PlaceholderText = "Precio unitario", Text = "0" };

        var panel = new StackPanel { Spacing = 10 };
        void Lbl(string t) => panel.Children.Add(new TextBlock { Text = t, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        Lbl("Descripción *"); panel.Children.Add(txtDesc);
        Lbl("Cantidad *");    panel.Children.Add(txtQty);
        Lbl("Precio *");      panel.Children.Add(txtPrecio);

        var dialog = new ContentDialog
        {
            Title = "Agregar Material / Servicio", PrimaryButtonText = "Agregar", SecondaryButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot, Content = panel
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var desc = txtDesc.Text.Trim();
        if (string.IsNullOrEmpty(desc)) { ViewModel.ErrorMessage = "Descripción obligatoria."; return; }
        if (!decimal.TryParse(txtQty.Text, out var qty) || qty <= 0) { ViewModel.ErrorMessage = "Cantidad inválida."; return; }
        if (!decimal.TryParse(txtPrecio.Text, out var precio) || precio < 0) { ViewModel.ErrorMessage = "Precio inválido."; return; }

        await ViewModel.AgregarMaterialLocalAsync(new DetalleLineaEditable(0, null, desc, qty, precio));
    }

    private async void BtnCancelarOT_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeCancelar) { ViewModel.ErrorMessage = "Esta OT no se puede cancelar en su estado actual."; return; }
        var dialog = new ContentDialog
        {
            Title = "Confirmar cancelación", Content = "¿Cancelar esta orden de trabajo?",
            PrimaryButtonText = "Sí", SecondaryButtonText = "No",
            DefaultButton = ContentDialogButton.Secondary, XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.CancelarAsync();
    }

    private async void BtnMarcarEntregada_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeMarcarEntregada) { ViewModel.ErrorMessage = "Solo se puede entregar una OT en estado Terminada."; return; }
        var dialog = new ContentDialog
        {
            Title = "Marcar OT como Entregada",
            Content = $"¿Confirmar la entrega de la OT #{ViewModel.DocumentoId}? Esta acción registra la entrega al cliente.",
            PrimaryButtonText = "Sí, entregar", SecondaryButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.MarcarEntregadaCommand.ExecuteAsync(null);
    }
}

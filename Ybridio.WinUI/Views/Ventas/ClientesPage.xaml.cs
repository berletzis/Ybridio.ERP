using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels.Ventas;

namespace Ybridio.WinUI.Views.Ventas;

public sealed partial class ClientesPage : Page, ILiveContextReporter
{
    public ClientesViewModel ViewModel { get; }

    public ClientesPage()
    {
        ViewModel = App.Services.GetRequiredService<ClientesViewModel>();
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
        if (ViewModel.ClienteSeleccionado is not null) ViewModel.EditarCommand.Execute(null);
    }

    public void ReportLiveContext() => ViewModel.ReportLiveContext();

    private async void AbrirDialogoNuevoEditar(ClienteDto? cliente)
    {
        var txtNombre      = new TextBox { PlaceholderText = "Nombre del cliente", Text = cliente?.Nombre ?? string.Empty };
        var txtRfc         = new TextBox { PlaceholderText = "RFC (opcional)",     Text = cliente?.RFC ?? string.Empty };
        var txtEmail       = new TextBox { PlaceholderText = "Email (opcional)",   Text = cliente?.Email ?? string.Empty };
        var txtTelefono    = new TextBox { PlaceholderText = "Teléfono",           Text = cliente?.Telefono ?? string.Empty };
        var txtDireccion   = new TextBox { PlaceholderText = "Dirección",          Text = cliente?.Direccion ?? string.Empty };
        var txtNotas       = new TextBox { PlaceholderText = "Notas internas",     Text = cliente?.Notas ?? string.Empty, AcceptsReturn = true };
        var txtCredito     = new TextBox { PlaceholderText = "0.00",              Text = (cliente?.LimiteCredito ?? 0).ToString("F2") };

        var panel = new StackPanel { Spacing = 10 };
        void Lbl(string t) => panel.Children.Add(new TextBlock { Text = t, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        Lbl("Nombre *");         panel.Children.Add(txtNombre);
        Lbl("RFC");              panel.Children.Add(txtRfc);
        Lbl("Email");            panel.Children.Add(txtEmail);
        Lbl("Teléfono");         panel.Children.Add(txtTelefono);
        Lbl("Dirección");        panel.Children.Add(txtDireccion);
        Lbl("Notas internas");   panel.Children.Add(txtNotas);
        Lbl("Límite de crédito (0 = contado)"); panel.Children.Add(txtCredito);

        var dialog = new ContentDialog
        {
            Title               = cliente is null ? "Nuevo Cliente" : "Editar Cliente",
            PrimaryButtonText   = "Guardar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = new ScrollViewer { Content = panel, MaxHeight = 500, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var nombre = txtNombre.Text.Trim();
        if (string.IsNullOrEmpty(nombre)) { ViewModel.ErrorMessage = "El nombre es obligatorio."; return; }

        if (!decimal.TryParse(txtCredito.Text.Trim(), out var credito)) credito = 0;

        await ViewModel.GuardarAsync(cliente, nombre,
            string.IsNullOrWhiteSpace(txtRfc.Text)      ? null : txtRfc.Text.Trim(),
            string.IsNullOrWhiteSpace(txtEmail.Text)    ? null : txtEmail.Text.Trim(),
            string.IsNullOrWhiteSpace(txtTelefono.Text) ? null : txtTelefono.Text.Trim(),
            string.IsNullOrWhiteSpace(txtDireccion.Text)? null : txtDireccion.Text.Trim(),
            string.IsNullOrWhiteSpace(txtNotas.Text)    ? null : txtNotas.Text.Trim(),
            credito);
    }
}

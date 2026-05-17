using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Ybridio.Application.DTOs.Inventario;
using Ybridio.Application.Services.Inventario;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.ViewModels.Config;

namespace Ybridio.WinUI.Views.Config;

/// <summary>
/// Página de gestión de almacenes para la sucursal activa.
/// Parte del módulo Configuración Sucursal — tab Almacenes.
/// </summary>
public sealed partial class AlmacenesTiendaPage : Page
{
    public AlmacenesTiendaViewModel ViewModel { get; }

    public AlmacenesTiendaPage()
    {
        ViewModel = new AlmacenesTiendaViewModel(
            App.Services.GetRequiredService<IAlmacenService>(),
            App.Services.GetRequiredService<SessionService>());

        InitializeComponent();
        ViewModel.SolicitarNuevoEditar = MostrarDialogoNuevoEditarAsync;
        _ = ViewModel.LoadAsync();
    }

    private void ListaAlmacenes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView lv)
            ViewModel.AlmacenSeleccionado = lv.SelectedItem as AlmacenDto;
    }

    private void ListaAlmacenes_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.AlmacenSeleccionado is not null)
            MostrarDialogoNuevoEditarAsync(ViewModel.AlmacenSeleccionado);
    }

    private async void MostrarDialogoNuevoEditarAsync(AlmacenDto? original)
    {
        var txtNombre      = new TextBox { PlaceholderText = "Nombre del almacén *",          Text = original?.Nombre      ?? "" };
        var txtCodigo      = new TextBox { PlaceholderText = "Código corto (ej: PRINCIPAL)",   Text = original?.Codigo      ?? "" };
        var txtDescripcion = new TextBox { PlaceholderText = "Descripción (opcional)",          Text = original?.Descripcion ?? "" };

        var panel = new StackPanel { Spacing = 10, MinWidth = 340 };
        void Lbl(string t) => panel.Children.Add(
            new TextBlock { Text = t, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        Lbl("Nombre *");    panel.Children.Add(txtNombre);
        Lbl("Código");      panel.Children.Add(txtCodigo);
        Lbl("Descripción"); panel.Children.Add(txtDescripcion);

        var dialog = new ContentDialog
        {
            Title               = original is null ? "Nuevo Almacén" : "Editar Almacén",
            PrimaryButtonText   = original is null ? "Crear" : "Guardar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = panel,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var nombre = txtNombre.Text.Trim();
        if (string.IsNullOrEmpty(nombre))
        {
            ViewModel.ErrorMessage = "El nombre es obligatorio.";
            return;
        }

        await ViewModel.GuardarAlmacenAsync(
            original,
            nombre,
            string.IsNullOrWhiteSpace(txtCodigo.Text)      ? null : txtCodigo.Text.Trim(),
            string.IsNullOrWhiteSpace(txtDescripcion.Text)  ? null : txtDescripcion.Text.Trim());
    }
}

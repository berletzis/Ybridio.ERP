using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Ybridio.WinUI.Views.Ventas;

/// <summary>
/// Document Surface para Clientes (creación/edición).
/// ADR-030: Global Document Surface UX Migration - inline mode.
/// </summary>
public sealed partial class ClienteDocumentoPage : Page
{
    private readonly SessionService _session;
    private readonly ClienteDto? _original;

    public string TituloDocumento => _original != null ? "Editar Cliente" : "Nuevo Cliente";

    // Callbacks para ClientesPage
    public Func<ClienteDto?, string, string?, string?, string?, string?, string?, decimal, Task<bool>>? GuardarAsync { get; set; }
    public Action? DocumentSaved { get; set; }
    public Action? VolverALista { get; set; }
    public Action? ToggleDetach { get; set; }

    public ClienteDocumentoPage(ClienteDto? cliente)
    {
        InitializeComponent();
        _session = App.Services.GetRequiredService<SessionService>();
        _original = cliente;

        // Rellenar campos si estamos editando
        if (_original != null)
        {
            TxtNombre.Text = _original.Nombre ?? string.Empty;
            TxtRFC.Text = _original.RFC ?? string.Empty;
            TxtEmail.Text = _original.Email ?? string.Empty;
            TxtTelefono.Text = _original.Telefono ?? string.Empty;
            TxtDireccion.Text = _original.Direccion ?? string.Empty;
            TxtNotas.Text = _original.Notas ?? string.Empty;
            TxtLimiteCredito.Text = _original.LimiteCredito > 0 
                ? _original.LimiteCredito.ToString("F2") 
                : "0.00";
        }
    }

    private void BtnVolverALista_Click(object sender, RoutedEventArgs e)
    {
        VolverALista?.Invoke();
    }

    private void BtnToggleDetach_Click(object sender, RoutedEventArgs e)
    {
        ToggleDetach?.Invoke();
    }

    private async void BtnAbrirEnVentana_Click(object sender, RoutedEventArgs e)
    {
        // ADR-030: Window Detach Mode - pendiente integración con WindowManager
        var dialog = new ContentDialog
        {
            Title = "Función en desarrollo",
            Content = "La apertura en ventana independiente estará disponible en la próxima actualización.",
            CloseButtonText = "Entendido",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        // Validación básica
        TxtMensaje.Visibility = Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(TxtNombre.Text))
        {
            TxtMensaje.Text = "El nombre del cliente es obligatorio.";
            TxtMensaje.Visibility = Visibility.Visible;
            TxtMensaje.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            return;
        }

        decimal limiteCredito = 0;
        if (!string.IsNullOrWhiteSpace(TxtLimiteCredito.Text) 
            && !decimal.TryParse(TxtLimiteCredito.Text, out limiteCredito))
        {
            limiteCredito = 0;
        }

        var rfc = string.IsNullOrWhiteSpace(TxtRFC.Text) ? null : TxtRFC.Text.Trim();
        var email = string.IsNullOrWhiteSpace(TxtEmail.Text) ? null : TxtEmail.Text.Trim();
        var telefono = string.IsNullOrWhiteSpace(TxtTelefono.Text) ? null : TxtTelefono.Text.Trim();
        var direccion = string.IsNullOrWhiteSpace(TxtDireccion.Text) ? null : TxtDireccion.Text.Trim();
        var notas = string.IsNullOrWhiteSpace(TxtNotas.Text) ? null : TxtNotas.Text.Trim();

        if (GuardarAsync != null)
        {
            var exito = await GuardarAsync(
                _original,
                TxtNombre.Text.Trim(),
                rfc,
                email,
                telefono,
                direccion,
                notas,
                limiteCredito);

            if (exito)
            {
                DocumentSaved?.Invoke();
            }
        }
    }
}

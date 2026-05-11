using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.Application.DTOs.Inventario;
using Ybridio.Application.Services.Inventario;
using Ybridio.Application.Services.Producto;
using Ybridio.Application.Services.Venta;
using Ybridio.Domain.Ventas;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Windowing;
using Ybridio.WinUI.Services.Workspace;
using Ybridio.WinUI.ViewModels.Ventas;
using Ybridio.WinUI.Views.Detached;
using ClienteDto     = Ybridio.Application.DTOs.Ventas.ClienteDto;
using CotizacionDto  = Ybridio.Application.DTOs.Ventas.CotizacionDto;
using PedidoDto      = Ybridio.Application.DTOs.Ventas.PedidoDto;
using XamlApp        = Microsoft.UI.Xaml.Application;

namespace Ybridio.WinUI.Views.Ventas;

/// <summary>
/// Documento de cotización persistente en WorkspaceService.
/// Soporta nueva cotización (flujo completo) y edición de existente.
/// Integra selección real de Cliente y Producto del catálogo ERP.
/// </summary>
public sealed partial class CotizacionDocumentoPage : Page
{
    private readonly IWorkspaceService         _workspace;
    private readonly IProductoService          _productoService;
    private readonly IInventarioService        _inventarioService;
    private readonly SessionService            _session;
    private readonly IWindowManager            _windowManager;         // ADR-029: Centralized Window Management
    private readonly CotizacionDto?            _cotizacionOriginal;    // ADR-028: snapshot para ventanas detached
    public CotizacionDocumentoViewModel ViewModel { get; }

    public CotizacionDocumentoPage(CotizacionDto? cotizacion)
    {
        ViewModel = new CotizacionDocumentoViewModel(
            App.Services.GetRequiredService<ICotizacionService>(),
            App.Services.GetRequiredService<IClienteService>(),
            App.Services.GetRequiredService<Ybridio.Application.Services.Autorizacion.IErpAuthorizationService>(),
            App.Services.GetRequiredService<SessionService>(),
            App.Services.GetRequiredService<Ybridio.WinUI.Services.Diagnostic.IOperationalObservabilityService>(),
            App.Services.GetRequiredService<Ybridio.WinUI.Services.Diagnostic.ICurrentContextTracker>());

        _workspace              = App.Services.GetRequiredService<IWorkspaceService>();
        _productoService        = App.Services.GetRequiredService<IProductoService>();
        _inventarioService      = App.Services.GetRequiredService<IInventarioService>();
        _session                = App.Services.GetRequiredService<SessionService>();
        _windowManager          = App.Services.GetRequiredService<IWindowManager>();
        _cotizacionOriginal     = cotizacion; // Guardar snapshot original

        InitializeComponent();

        ViewModel.NotificarPedidoGenerado = AbrirPedidoEnWorkspace;
        ViewModel.Initialize(cotizacion);
    }

    /// <summary>
    /// Callback invocado cuando el usuario hace clic en "← Volver a Lista".
    /// Usado por el Document Surface UX Pattern para cerrar el surface y volver al grid.
    /// </summary>
    public Action? VolverALista { get; set; }

    /// <summary>
    /// Callback invocado cuando el usuario hace clic en "Desacoplar Surface" (ADR-027).
    /// Alterna entre modo acoplado (content replacement) y modo desacoplado (split view).
    /// Permite multitarea ligera controlada sin volver a Workspace Tabs infinitos.
    /// </summary>
    public Action? ToggleDetach { get; set; }

    private void BtnVolverALista_Click(object sender, RoutedEventArgs e)
    {
        VolverALista?.Invoke();
    }

    // ── Document Surface Detachable Mode (ADR-027) ───────────────────────────

    private void BtnToggleDetach_Click(object sender, RoutedEventArgs e)
    {
        ToggleDetach?.Invoke();
    }

    // ── Document Surface Window Detach Mode (ADR-028 + ADR-029) ──────────────

    /// <summary>
    /// Abre el documento de cotización actual en una ventana OS real independiente.
    /// LIMITACIÓN ARQUITECTÓNICA: Máximo 2 ventanas desacopladas simultáneas (ADR-028).
    /// Usa WindowManager centralizado (ADR-029) con key prefix "detached:" para policy enforcement.
    /// Crea nueva instancia del documento page con snapshot original del DTO para evitar conflictos de state.
    /// </summary>
    private void BtnAbrirEnVentana_Click(object sender, RoutedEventArgs e)
    {
        var titulo = _cotizacionOriginal is not null
            ? $"Cotización - {_cotizacionOriginal.NombreCliente}"
            : "Nueva Cotización";

        // Key con prefix "detached:" activa policy de límite máximo 2 ventanas en WindowManager
        var cotizacionId = _cotizacionOriginal?.Id.ToString() ?? Guid.NewGuid().ToString();
        var detachedKey  = $"detached:cotizacion:{cotizacionId}";

        try
        {
            _windowManager.OpenWindow<DetachedDocumentWindow, string>(
                key: detachedKey,
                factory: () =>
                {
                    // Crear nueva instancia de la página con snapshot del documento original
                    // IMPORTANTE: Cada ventana tiene su propia instancia de página y ViewModel para evitar conflictos de state
                    var nuevaPagina = new CotizacionDocumentoPage(_cotizacionOriginal);
                    return new DetachedDocumentWindow(nuevaPagina, titulo);
                },
                options: new WindowOptions
                {
                    Width  = 1200,
                    Height = 800,
                    PositionStrategy = WindowPositionStrategy.CenterScreen
                });
        }
        catch (DetachedWindowLimitException ex)
        {
            // Mostrar mensaje operacional al usuario cuando límite es alcanzado
            _ = MostrarMensajeLimiteVentanasAsync(ex);
        }
    }

    /// <summary>
    /// Muestra dialog operacional cuando se alcanza el límite de ventanas detached (máximo 2).
    /// </summary>
    private async System.Threading.Tasks.Task MostrarMensajeLimiteVentanasAsync(DetachedWindowLimitException ex)
    {
        var dialog = new ContentDialog
        {
            Title           = "Límite de ventanas alcanzado",
            Content         = ex.Message,
            CloseButtonText = "Entendido",
            XamlRoot        = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void AbrirPedidoEnWorkspace(PedidoDto pedido)
    {
        _ = _workspace.OpenOrActivateDocumentTabAsync(
            key:         $"pedido-{pedido.Id}",
            title:       $"Pedido #{pedido.Id}",
            icon:        "",
            dataLoader:  () => System.Threading.Tasks.Task.FromResult<PedidoDto?>(pedido),
            pageFactory: dto => new PedidoDocumentoPage(dto!),
            isClosable:  true);
    }

    // ── Handlers AutoSuggestBox de Cliente ───────────────────────────────────

    private async void ClienteAutoSuggest_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // Solo buscar cuando el usuario escribe (no cuando el sistema cambia el texto)
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

        var texto = sender.Text;
        if (string.IsNullOrWhiteSpace(texto) || texto.Length < 2)
        {
            ViewModel.LimpiarCliente();
            sender.ItemsSource = null;
            return;
        }
        await ViewModel.BuscarClientesAsync(texto);
        sender.ItemsSource = ViewModel.SugerenciasCliente;
    }

    private void ClienteAutoSuggest_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is ClienteDto cliente)
        {
            ViewModel.SeleccionarCliente(cliente);
            sender.Text = cliente.Nombre;
        }
    }

    private void ClienteAutoSuggest_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is ClienteDto cliente)
            ViewModel.SeleccionarCliente(cliente);
    }

    // ── Handlers de CommandBar ────────────────────────────────────────────────

    private async void BtnAgregarLinea_Click(object sender, RoutedEventArgs e)
    {
        var detalle = await MostrarDialogoNuevaLinea();
        if (detalle is null) return;
        await ViewModel.AgregarDetalleLocalAsync(detalle);
    }

    private async void BtnEnviar_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeEnviar) { ViewModel.ErrorMessage = "Solo se puede enviar una cotización en estado Borrador."; return; }
        await ViewModel.CambiarEstatusCommand.ExecuteAsync(EstatusCotizacion.Enviada);
    }

    private async void BtnAprobar_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeAprobar) { ViewModel.ErrorMessage = "Solo se puede aprobar una cotización Enviada."; return; }
        await ViewModel.CambiarEstatusCommand.ExecuteAsync(EstatusCotizacion.Aprobada);
    }

    private async void BtnCancelarCotizacion_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeCancelar) { ViewModel.ErrorMessage = "Esta cotización no se puede cancelar en su estado actual."; return; }
        var dialog = new ContentDialog
        {
            Title = "Confirmar cancelación",
            Content = "¿Cancelar esta cotización? Esta acción no es reversible.",
            PrimaryButtonText = "Sí, cancelar", SecondaryButtonText = "No",
            DefaultButton = ContentDialogButton.Secondary, XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.CambiarEstatusCommand.ExecuteAsync(EstatusCotizacion.Cancelada);
    }

    // ── Diálogo: Agregar Línea con Producto real ──────────────────────────────

    private async System.Threading.Tasks.Task<DetalleLineaEditable?> MostrarDialogoNuevaLinea()
    {
        // ── Controles del diálogo ─────────────────────────────────────────────
        var asbProducto = new AutoSuggestBox
        {
            PlaceholderText = "Buscar por código o nombre (mínimo 2 caracteres)...",
            QueryIcon       = new SymbolIcon(Symbol.Find)
        };
        var txtDescripcion    = new TextBox { PlaceholderText = "Descripción (se carga al seleccionar producto)" };
        var txtCantidad       = new TextBox { PlaceholderText = "Cantidad", Text = "1" };
        var txtPrecioUnitario = new TextBox { PlaceholderText = "Precio unitario", Text = "0" };

        // Existencia informativa — no reserva stock (§3 requerimiento: solo mostrar)
        var txbInfoProducto = new TextBlock
        {
            Text         = "",
            FontSize     = 11,
            Foreground   = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
        };

        ProductoDto? productoSeleccionado = null;

        // ── Búsqueda de producto ──────────────────────────────────────────────
        asbProducto.TextChanged += async (s, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            var q = s.Text.Trim();
            if (q.Length < 2) { s.ItemsSource = null; return; }
            var resultados = await _productoService.BuscarAsync(_session.EmpresaId, q);
            s.ItemsSource = resultados;
        };

        asbProducto.SuggestionChosen += async (s, args) =>
        {
            if (args.SelectedItem is not ProductoDto p) return;
            productoSeleccionado   = p;
            s.Text                 = $"{p.Codigo} — {p.Nombre}";
            txtDescripcion.Text    = p.Nombre;
            txtPrecioUnitario.Text = p.Precio.ToString("F2");

            // Existencia informativa: suma total por empresa (sin filtro de almacén)
            // NO reserva stock — solo orientativo para el vendedor (§3 requerimiento)
            var existencias     = await _inventarioService.ListarExistenciasAsync(_session.EmpresaId);
            var totalExistencia = existencias.Where(e => e.ProductoId == p.Id).Sum(e => e.Cantidad);

            txbInfoProducto.Text = totalExistencia > 0
                ? $"Existencia disponible: {totalExistencia:N2} {p.UnidadMedidaAbreviatura ?? ""}"
                : "Sin existencia registrada para este producto.";
        };

        // ── Panel del diálogo ─────────────────────────────────────────────────
        var panel = new StackPanel { Spacing = 8, MinWidth = 380 };
        void Lbl(string t) => panel.Children.Add(new TextBlock
        {
            Text       = t,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize   = 12
        });

        Lbl("Producto *");
        panel.Children.Add(asbProducto);
        panel.Children.Add(txbInfoProducto);
        Lbl("Descripción *");
        panel.Children.Add(txtDescripcion);
        Lbl("Cantidad *");
        panel.Children.Add(txtCantidad);
        Lbl("Precio Unitario *");
        panel.Children.Add(txtPrecioUnitario);

        var dialog = new ContentDialog
        {
            Title               = "Agregar Línea",
            PrimaryButtonText   = "Agregar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = panel
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;

        // ── Validaciones (§7 requerimiento) ──────────────────────────────────
        if (productoSeleccionado is null)
        {
            ViewModel.ErrorMessage = "Debe seleccionar un producto del catálogo.";
            return null;
        }
        var desc = txtDescripcion.Text.Trim();
        if (string.IsNullOrEmpty(desc)) { ViewModel.ErrorMessage = "La descripción es obligatoria."; return null; }
        if (!decimal.TryParse(txtCantidad.Text.Trim(), out var qty) || qty <= 0)
        {
            ViewModel.ErrorMessage = "Cantidad inválida. Debe ser mayor a 0.";
            return null;
        }
        if (!decimal.TryParse(txtPrecioUnitario.Text.Trim(), out var precio) || precio < 0)
        {
            ViewModel.ErrorMessage = "Precio unitario inválido.";
            return null;
        }

        // Recuperar existencia final para persistirla en la línea (solo informativa)
        var existenciasFinales = await _inventarioService.ListarExistenciasAsync(_session.EmpresaId);
        var existenciaTotal = existenciasFinales
            .Where(e => e.ProductoId == productoSeleccionado.Id)
            .Sum(e => e.Cantidad);

        return new DetalleLineaEditable(
            id:                   0,
            productoId:           productoSeleccionado.Id,
            descripcion:          desc,
            cantidad:             qty,
            precioUnitario:       precio,
            sku:                  productoSeleccionado.Codigo,
            existenciaDisponible: existenciaTotal > 0 ? existenciaTotal : null);
    }
}

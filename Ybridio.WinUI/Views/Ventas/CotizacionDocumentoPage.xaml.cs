using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.Application.DTOs.Directorio;
using Ybridio.Application.DTOs.Inventario;
using Ybridio.Application.Services.Directorio;
using Ybridio.Application.Services.Inventario;
using Ybridio.Application.Services.Producto;
using Ybridio.Application.Services.Venta;
using Ybridio.Domain.Ventas;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Windowing;
using Ybridio.WinUI.Services.Workspace;
using Ybridio.WinUI.ViewModels.Ventas;
using Ybridio.WinUI.Views.Detached;
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
    private readonly CotizacionDto?            _cotizacionOriginal;    // ADR-039: referencia para window key y título
    public CotizacionDocumentoViewModel ViewModel { get; }

    public CotizacionDocumentoPage(CotizacionDto? cotizacion)
    {
        ViewModel = new CotizacionDocumentoViewModel(
            App.Services.GetRequiredService<ICotizacionService>(),
            App.Services.GetRequiredService<IRelacionComercialService>(),
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
    /// Constructor de rehost (ADR-039 — Shared ViewModel Pattern).
    /// Crea una nueva Page en la ventana desacoplada reutilizando el ViewModel existente.
    /// NO llama a Initialize() para preservar todo el estado runtime sin pérdida.
    /// El chip del selector se restaura desde ViewModel.EntidadDirectorioSeleccionada.
    /// </summary>
    internal CotizacionDocumentoPage(CotizacionDocumentoViewModel viewModelExistente, CotizacionDto? cotizacionOriginal)
    {
        ViewModel = viewModelExistente;

        _workspace          = App.Services.GetRequiredService<IWorkspaceService>();
        _productoService    = App.Services.GetRequiredService<IProductoService>();
        _inventarioService  = App.Services.GetRequiredService<IInventarioService>();
        _session            = App.Services.GetRequiredService<SessionService>();
        _windowManager      = App.Services.GetRequiredService<IWindowManager>();
        _cotizacionOriginal = cotizacionOriginal;

        InitializeComponent();

        // Restaurar chip del selector desde el ViewModel (ADR-039)
        // x:Bind en el XAML enlaza la lógica de negocio del ViewModel correctamente.
        // El selector control necesita que se le pase la entidad seleccionada explícitamente
        // porque es un UserControl con DependencyProperty propio, no enlazado vía x:Bind TwoWay.
        if (ViewModel.EntidadDirectorioSeleccionada is not null)
            SelectorCliente.EntidadSeleccionada = ViewModel.EntidadDirectorioSeleccionada;

        ViewModel.NotificarPedidoGenerado = AbrirPedidoEnWorkspace;
    }

    /// <summary>
    /// Callback invocado cuando el usuario hace clic en "← Volver a Lista".
    /// Usado por el Document Surface UX Pattern (ADR-032) para cerrar el surface y volver al grid.
    /// </summary>
    public Action? VolverALista { get; set; }

    /// <summary>
    /// Indica si el documento está embebido inline dentro del módulo (true) o en ventana standalone (false).
    /// Controla la visibilidad de "Volver a Lista" y "Abrir en nueva ventana" en el CommandBar.
    /// </summary>
    public bool EsInlineMode
    {
        get => _esInlineMode;
        set
        {
            _esInlineMode = value;
            var vis = value ? Visibility.Visible : Visibility.Collapsed;
            BtnVolverALista.Visibility   = vis;
            BtnAbrirEnVentana.Visibility = vis;
        }
    }
    private bool _esInlineMode;

    private async void BtnVolverALista_Click(object sender, RoutedEventArgs e)
    {
        if (!await MostrarConfirmacionCierreAsync()) return;
        VolverALista?.Invoke();
    }

    // ── Window Mode (ADR-032 / ADR-039) ──────────────────────────────────────

    /// <summary>
    /// Mueve el documento actual a una ventana OS real independiente (ADR-039: Shared Document Session Pattern).
    /// Rehostea ESTA página (misma instancia, mismo ViewModel, estado runtime completo) en la ventana desacoplada.
    /// NO recrea el documento. NO pierde estado. NO hace auto-save.
    /// </summary>
    private void BtnAbrirEnVentana_Click(object sender, RoutedEventArgs e)
    {
        // Título construido desde el runtime state actual del ViewModel (no desde snapshot estático)
        var titulo = !string.IsNullOrWhiteSpace(ViewModel.NombreCliente)
            ? $"Cotización - {ViewModel.NombreCliente}"
            : (ViewModel.IsNuevo ? "Nueva Cotización" : $"Cotización #{_cotizacionOriginal?.Id}");

        var cotizacionId = _cotizacionOriginal?.Id.ToString() ?? _sessionKey;
        var windowKey    = $"detached:cotizacion:{cotizacionId}";

        // ADR-039 — Shared ViewModel Pattern para WinUI 3:
        // WinUI 3 no permite mover un UIElement entre ventanas (límite de compositor).
        // La solución correcta es crear una nueva Page en la nueva ventana pasando el ViewModel
        // existente. El estado runtime (líneas, cliente, dirty, totales) vive en el ViewModel
        // y se preserva completamente. Solo el selector chip requiere restauración explícita.
        var viewModelActual    = ViewModel;
        var cotizacionOriginal = _cotizacionOriginal;

        try
        {
            // Cerrar inline surface: el módulo vuelve al grid
            VolverALista?.Invoke();

            _windowManager.OpenWindow<DetachedDocumentWindow, string>(
                key: windowKey,
                factory: () =>
                {
                    // Nueva Page + mismo ViewModel existente = misma sesión documental
                    var paginaRehost = new CotizacionDocumentoPage(viewModelActual, cotizacionOriginal);
                    return new DetachedDocumentWindow(paginaRehost, titulo);
                },
                options: new WindowOptions
                {
                    Width            = 1200,
                    Height           = 800,
                    PositionStrategy = WindowPositionStrategy.CenterScreen
                });
        }
        catch (DetachedWindowLimitException ex)
        {
            _ = MostrarMensajeLimiteVentanasAsync(ex);
        }
    }

    // Clave de sesión única para cotizaciones nuevas (sin Id asignado aún)
    private readonly string _sessionKey = Guid.NewGuid().ToString();

    // ── Protección de cierre (ADR-040) ────────────────────────────────────────

    /// <summary>
    /// Muestra el diálogo institucional de confirmación al cerrar un documento con cambios no guardados.
    /// Devuelve <c>true</c> si el usuario decidió guardar o descartar; <c>false</c> si canceló (no cerrar).
    /// </summary>
    public async Task<bool> MostrarConfirmacionCierreAsync()
    {
        if (!ViewModel.IsDirty) return true;

        var dialog = new ContentDialog
        {
            Title               = "Cambios sin guardar",
            Content             = "Existen cambios sin guardar. ¿Desea cerrar y perder los cambios?",
            PrimaryButtonText   = "Guardar",
            SecondaryButtonText = "No guardar",
            CloseButtonText     = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // Guardar antes de cerrar
            await ViewModel.GuardarCommand.ExecuteAsync(null);
            return true;
        }
        if (result == ContentDialogResult.Secondary)
            return true;  // No guardar — cerrar de todas formas

        return false;  // Cancelar — no cerrar
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

    // ── Handler RelacionComercialSelectorControl (ADR-038) ─────────────────

    private void SelectorCliente_SelectionChanged(object? sender, DirectorioSelectorDto? entidad)
    {
        if (entidad is not null)
            ViewModel.SeleccionarCliente(entidad);
        else
            ViewModel.LimpiarCliente();
    }

    // ── Handlers de CommandBar ────────────────────────────────────────────────

    /// <summary>
    /// Edición inline de cantidad (ADR-041 — Operational Editable Document Lines Pattern).
    /// Para documentos NUEVOS: el binding TwoWay en CantidadDouble ya aplica el cambio y recalcula vía INPC.
    /// Para documentos EXISTENTES: además persiste el detalle en BD a través de ActualizarCantidadAsync.
    /// </summary>
    private async void NumberBox_Cantidad_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue)) return;
        if (args.NewValue == args.OldValue) return;
        if (sender.Tag is not ViewModels.Ventas.DetalleLineaEditable linea) return;

        var nuevaCantidad = (decimal)args.NewValue;

        // Para documentos existentes: persistir en BD (el INPC del TwoWay ya actualizó la UI)
        if (!ViewModel.IsNuevo)
            await ViewModel.ActualizarCantidadAsync(linea, nuevaCantidad);
        // Para nuevos: el setter CantidadDouble → SetCantidad ya disparó INPC + callback
    }

    private async void BtnAgregarLinea_Click(object sender, RoutedEventArgs e)
    {
        var detalle = await MostrarDialogoNuevaLinea();
        if (detalle is null) return;
        // ADR-040: merge-or-add — nunca crear líneas duplicadas del mismo producto
        await ViewModel.AgregarOIncrementarDetalleAsync(detalle);
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
            existenciaDisponible: existenciaTotal > 0 ? existenciaTotal : null,
            ivaAplicable:         productoSeleccionado.IvaAplicable);
    }
}

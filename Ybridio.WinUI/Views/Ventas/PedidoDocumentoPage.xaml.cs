using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.Application.DTOs.Directorio;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Directorio;
using System.Linq;
using System.Threading;
using Ybridio.Application.Services.Configuracion;
using Ybridio.Application.Services.Inventario;
using Ybridio.Application.Services.Producto;
using Ybridio.Application.Services.Venta;
using Ybridio.Domain.Ventas;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Windowing;
using Ybridio.WinUI.Services.Workspace;
using Ybridio.WinUI.ViewModels.Ventas;
using Ybridio.WinUI.Helpers;
using Ybridio.WinUI.Views.Detached;

namespace Ybridio.WinUI.Views.Ventas;

public sealed partial class PedidoDocumentoPage : Page
{
    private readonly IWorkspaceService _workspace;
    private readonly IWindowManager    _windowManager;
    private readonly string            _sessionKey = Guid.NewGuid().ToString();

    public PedidoDocumentoViewModel ViewModel { get; }

    /// <summary>
    /// Callback invocado al cerrar el Document Surface e ir al listado (ADR-031).
    /// Equivalente a VolverALista en CotizacionDocumentoPage.
    /// </summary>
    public Action? VolverALista { get; set; }

    /// <summary>
    /// Indica si el documento está embebido inline dentro del módulo (true) o en ventana standalone (false).
    /// Controla la visibilidad de "Volver a Lista", separador y "Abrir en ventana".
    /// </summary>
    public bool EsInlineMode
    {
        get => _esInlineMode;
        set
        {
            _esInlineMode = value;
            var vis = value ? Visibility.Visible : Visibility.Collapsed;
            BtnVolverALista.Visibility   = vis;
            SepInlineActions.Visibility  = vis;
            SepVentana.Visibility        = vis;
            BtnAbrirEnVentana.Visibility = vis;
            // HeaderStrip es SIEMPRE visible — muestra el folio institucional en todos los modos.
            // Solo los controles de navegación inline (BtnVolverALista, etc.) se ocultan en modo standalone.
        }
    }
    private bool _esInlineMode;

    private readonly IOtroCargoService   _otroCargoService;
    private readonly IDirectorioService  _directorioService;
    private readonly IProductoService    _productoService;
    private readonly IInventarioService  _inventarioService;
    private readonly SessionService      _session;

    /// <summary>
    /// Bloquea todos los NumberBox handlers durante el ciclo de inicialización/render.
    /// Se activa en true vía Page.Loaded — cualquier ValueChanged antes de Loaded es
    /// un evento de binding inicial, NO una acción del usuario.
    /// </summary>
    private bool _listaParaEdicion;

    // ── Constructor principal ────────────────────────────────────────────────
    public PedidoDocumentoPage(PedidoDto? pedido)
    {
        ViewModel          = CreateViewModel();
        _workspace          = App.Services.GetRequiredService<IWorkspaceService>();
        _windowManager      = App.Services.GetRequiredService<IWindowManager>();
        _otroCargoService   = App.Services.GetRequiredService<IOtroCargoService>();
        _directorioService  = App.Services.GetRequiredService<IDirectorioService>();
        _productoService    = App.Services.GetRequiredService<IProductoService>();
        _inventarioService  = App.Services.GetRequiredService<IInventarioService>();
        _session            = App.Services.GetRequiredService<SessionService>();

        // Bloquear handlers hasta que la página esté completamente renderizada
        _listaParaEdicion = false;
        Loaded += (_, _) => _listaParaEdicion = true;

        InitializeComponent();
        ViewModel.NotificarOTGenerada    = AbrirOTEnWorkspace;
        ViewModel.NotificarVentaGenerada = AbrirVentaEnWorkspace;
        ViewModel.SolicitarAgregarCargo  = AbrirDialogoAgregarCargo;
        ViewModel.Initialize(pedido);

        // Chip sintético inmediato (solo nombre)
        if (ViewModel.EntidadDirectorioSeleccionada is not null)
            SelectorCliente.EntidadSeleccionada = ViewModel.EntidadDirectorioSeleccionada;

        // Hydration async: email + teléfono real
        if (pedido?.RelacionComercialId is int relId)
            HidratarSelectorClienteAsync(relId)
                .FireAndForget(err => ViewModel.ErrorMessage = $"Hydration cliente: {err}");

        ViewModel.CargarConfiguracionFiscalAsync()
            .FireAndForget();
    }

    /// <summary>
    /// Constructor de rehost (ADR-039 — Shared ViewModel Pattern).
    /// Reutiliza el ViewModel existente — preserva estado runtime completo sin llamar Initialize.
    /// </summary>
    internal PedidoDocumentoPage(PedidoDocumentoViewModel viewModelExistente)
    {
        ViewModel           = viewModelExistente;
        _workspace          = App.Services.GetRequiredService<IWorkspaceService>();
        _windowManager      = App.Services.GetRequiredService<IWindowManager>();
        _otroCargoService   = App.Services.GetRequiredService<IOtroCargoService>();
        _directorioService  = App.Services.GetRequiredService<IDirectorioService>();
        _productoService    = App.Services.GetRequiredService<IProductoService>();
        _inventarioService  = App.Services.GetRequiredService<IInventarioService>();
        _session            = App.Services.GetRequiredService<SessionService>();
        _listaParaEdicion   = false;
        Loaded += (_, _) => _listaParaEdicion = true;
        InitializeComponent();
        ViewModel.NotificarOTGenerada    = AbrirOTEnWorkspace;
        ViewModel.NotificarVentaGenerada = AbrirVentaEnWorkspace;
        ViewModel.SolicitarAgregarCargo  = AbrirDialogoAgregarCargo;

        // Restaurar chip del selector (rehost preserva ViewModel.EntidadDirectorioSeleccionada)
        if (ViewModel.EntidadDirectorioSeleccionada is not null)
            SelectorCliente.EntidadSeleccionada = ViewModel.EntidadDirectorioSeleccionada;
    }

    private static PedidoDocumentoViewModel CreateViewModel() =>
        new(App.Services.GetRequiredService<IPedidoService>(),
            App.Services.GetRequiredService<IVentaDocumentalService>(),
            App.Services.GetRequiredService<IRelacionComercialService>(),
            App.Services.GetRequiredService<Ybridio.Application.Services.Autorizacion.IErpAuthorizationService>(),
            App.Services.GetRequiredService<SessionService>(),
            App.Services.GetRequiredService<Ybridio.WinUI.Services.Diagnostic.IOperationalObservabilityService>(),
            App.Services.GetRequiredService<Ybridio.WinUI.Services.Diagnostic.ICurrentContextTracker>(),
            App.Services.GetRequiredService<Ybridio.Application.Services.Configuracion.IConfiguracionFiscalService>());

    private void AbrirOTEnWorkspace(OrdenTrabajoDto ot)
    {
        _ = _workspace.OpenOrActivateDocumentTabAsync(
            key:         $"ot-{ot.Id}",
            title:       $"OT #{ot.Id}",
            icon:        "",
            dataLoader:  () => System.Threading.Tasks.Task.FromResult<OrdenTrabajoDto?>(ot),
            pageFactory: dto => new OrdenTrabajoDocumentoPage(dto!),
            isClosable:  true);
    }

    private void AbrirVentaEnWorkspace(VentaDocumentalDto venta)
    {
        _ = _workspace.OpenOrActivateDocumentTabAsync(
            key:         $"venta-{venta.Id}",
            title:       $"Venta #{venta.Id}",
            icon:        "",
            dataLoader:  () => System.Threading.Tasks.Task.FromResult<VentaDocumentalDto?>(venta),
            pageFactory: dto => new VentaDocumentoPage(dto!),
            isClosable:  true);
    }

    private void BtnVolverALista_Click(object sender, RoutedEventArgs e)
        => VolverALista?.Invoke();

    // ── Window Mode (ADR-039 — Shared Document Session Pattern) ──────────────

    /// <summary>
    /// Mueve el documento actual a una ventana OS real independiente.
    /// Rehostea ESTA página (misma instancia, mismo ViewModel) en la ventana desacoplada.
    /// Global Document Runtime Ownership: window key usa DocumentoId real o _sessionKey.
    /// </summary>
    private void BtnAbrirEnVentana_Click(object sender, RoutedEventArgs e)
    {
        var titulo = !string.IsNullOrWhiteSpace(ViewModel.NombreCliente)
            ? $"Pedido — {ViewModel.NombreCliente}"
            : (ViewModel.IsNuevo
                ? "Nuevo Pedido"
                : $"Pedido {(ViewModel.Folio ?? "#" + ViewModel.DocumentoId)}");

        var pedidoId  = ViewModel.DocumentoId?.ToString() ?? _sessionKey;
        var windowKey = $"detached:pedido:{pedidoId}";
        var vmActual  = ViewModel;

        try
        {
            VolverALista?.Invoke();

            _windowManager.OpenWindow<DetachedDocumentWindow, string>(
                key:     windowKey,
                factory: () =>
                {
                    var rehost = new PedidoDocumentoPage(vmActual);
                    return new DetachedDocumentWindow(rehost, titulo);
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

    private async System.Threading.Tasks.Task MostrarMensajeLimiteVentanasAsync(DetachedWindowLimitException ex)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = "Límite de ventanas alcanzado", Content = ex.Message,
                CloseButtonText = "Entendido", XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch { /* XamlRoot puede no estar listo — ignorar */ }
    }

    // ── Hydration del selector de cliente ────────────────────────────────────

    /// <summary>
    /// Carga el DirectorioSelectorDto completo (email, teléfono) desde BD para mostrar en el chip.
    /// Best-effort: si falla, el chip mantiene el nombre del DTO sintético.
    /// Mismo patrón que CotizacionDocumentoPage (Selector DTO Hydration Rule).
    /// </summary>
    private async Task HidratarSelectorClienteAsync(int relacionComercialId)
    {
        try
        {
            var dto = await _directorioService.ObtenerDtoParaSelectorAsync(
                relacionComercialId, CancellationToken.None);
            if (dto is null) return;
            ViewModel.RestaurarEntidadSeleccionada(dto);
            SelectorCliente.EntidadSeleccionada = dto;
        }
        catch { /* best-effort */ }
    }

    // ── CalendarDatePicker handlers ──────────────────────────────────────────

    private void DpFecha_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (args.NewDate.HasValue) ViewModel.FechaOffset = args.NewDate.Value;
    }

    private void DpEntrega_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        ViewModel.FechaEntregaCompromisoOffset = args.NewDate ?? DateTimeOffset.Now.AddDays(7);
    }

    // ── Descuento global (ADR-042) ────────────────────────────────────────────

    private bool _hidratandoDescuento;

    private void NbDescuentoGlobal_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_listaParaEdicion || _hidratandoDescuento || double.IsNaN(args.NewValue)) return;
        if (args.NewValue == args.OldValue) return;
        if (ViewModel.IsBusy) return;
        _ = ViewModel.AplicarDescuentoGlobalALineasAsync((decimal)args.NewValue);
    }

    // ── Cargos handlers ──────────────────────────────────────────────────────

    private void BtnEliminarCargo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CargoLineaEditable cargo)
        {
            ViewModel.CargoSeleccionado = cargo;
            _ = ViewModel.EliminarCargoCommand.ExecuteAsync(null);
        }
    }

    private async Task<CargoLineaEditable?> AbrirDialogoAgregarCargo()
    {
        IReadOnlyList<OtroCargoDto> catalogoCargos;
        try { catalogoCargos = await _otroCargoService.ListarAsync(); }
        catch { catalogoCargos = []; }

        var cmbCatalogo = new ComboBox { PlaceholderText = "Seleccionar del catálogo (opcional)", MinWidth = 300 };
        cmbCatalogo.Items.Add(new ComboBoxItem { Content = "(Cargo libre)", Tag = (OtroCargoDto?)null });
        foreach (var c in catalogoCargos)
            cmbCatalogo.Items.Add(new ComboBoxItem { Content = c.Nombre, Tag = c });
        cmbCatalogo.SelectedIndex = 0;

        var txtDesc  = new TextBox { PlaceholderText = "Descripción del cargo" };
        var nbImporte = new NumberBox
        {
            Value = 0, Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            NumberFormatter = new Windows.Globalization.NumberFormatting.CurrencyFormatter("MXN") { FractionDigits = 2 }
        };
        var chkIva = new CheckBox { Content = "Aplica IVA", IsChecked = false };

        cmbCatalogo.SelectionChanged += (_, _) =>
        {
            if (cmbCatalogo.SelectedItem is ComboBoxItem ci && ci.Tag is OtroCargoDto c)
            {
                txtDesc.Text    = c.Nombre;
                chkIva.IsChecked = c.AplicaIva;
            }
        };

        var panel = new StackPanel { Spacing = 10, MinWidth = 340 };
        panel.Children.Add(new TextBlock { Text = "Catálogo", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(cmbCatalogo);
        panel.Children.Add(new TextBlock { Text = "Descripción *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(txtDesc);
        panel.Children.Add(new TextBlock { Text = "Importe ($)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(nbImporte);
        panel.Children.Add(chkIva);

        var dialog = new ContentDialog
        {
            Title = "Agregar Cargo Accesorio", PrimaryButtonText = "Agregar", SecondaryButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot, Content = panel
        };

        ContentDialogResult result;
        try { result = await dialog.ShowAsync(); }
        catch (System.Runtime.InteropServices.COMException) { return null; }

        if (result != ContentDialogResult.Primary) return null;
        if (string.IsNullOrWhiteSpace(txtDesc.Text)) { ViewModel.ErrorMessage = "La descripción es requerida."; return null; }

        return new CargoLineaEditable(0, null, txtDesc.Text.Trim(), (decimal)nbImporte.Value, chkIva.IsChecked == true);
    }

    // ── NumberBox quantity/discount handlers (ADR-041 / ADR-043) ─────────────

    private async void NumberBox_Cantidad_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        // _listaParaEdicion = false hasta que Page.Loaded dispare — bloquea todos los eventos de inicialización
        if (!_listaParaEdicion) return;
        if (double.IsNaN(args.NewValue) || args.NewValue == args.OldValue) return;
        if (sender.Tag is not DetalleLineaEditable linea) return;
        if (ViewModel.IsBusy) return;
        await ViewModel.ActualizarCantidadAsync(linea, (decimal)args.NewValue);
    }

    private async void NumberBox_Descuento_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_listaParaEdicion) return;
        if (double.IsNaN(args.NewValue) || args.NewValue == args.OldValue) return;
        if (sender.Tag is not DetalleLineaEditable linea) return;
        if (ViewModel.IsBusy) return;
        await ViewModel.ActualizarDescuentoAsync(linea, (decimal)args.NewValue);
    }

    // ── Handler RelacionComercialSelectorControl (ADR-038) ───────────────────

    private void SelectorCliente_SelectionChanged(object? sender, DirectorioSelectorDto? entidad)
    {
        if (entidad is not null)
            ViewModel.SeleccionarCliente(entidad);
        else
            ViewModel.LimpiarCliente();
    }

    // ── Workflow Menu contextual ─────────────────────────────────────────────

    /// <summary>
    /// Construye dinámicamente el MenuFlyout con las transiciones válidas
    /// según el estado actual del pedido. NUNCA hardcodear estados en XAML.
    /// </summary>
    private void WorkflowFlyout_Opening(object sender, object e)
    {
        if (sender is not MenuFlyout flyout) return;
        flyout.Items.Clear();

        var transitions = ViewModel.GetAvailableTransitions();
        if (transitions.Count == 0) return;

        foreach (var (estado, etiqueta) in transitions)
        {
            var item = new MenuFlyoutItem { Text = etiqueta, Tag = estado };
            item.Click += WorkflowTransicion_Click;
            flyout.Items.Add(item);
        }

        // Cancelar siempre disponible como acción destructiva al final
        if (ViewModel.PuedeCancelar)
        {
            if (transitions.Count > 0) flyout.Items.Add(new MenuFlyoutSeparator());
            var cancelItem = new MenuFlyoutItem { Text = "Cancelar Pedido" };
            cancelItem.Click += BtnCancelarPedido_Click;
            flyout.Items.Add(cancelItem);
        }
    }

    private void BtnWorkflow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is AppBarButton btn) btn.Flyout?.ShowAt(btn);
    }

    private async void WorkflowTransicion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item || item.Tag is not EstatusPedido destino) return;
        await ViewModel.AvanzarAEstatusAsync(destino);
    }

    private async void BtnGenerarVenta_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeGenerarVenta) { ViewModel.ErrorMessage = "Este pedido no permite generar una venta."; return; }
        var dialog = new ContentDialog
        {
            Title = "Generar Venta desde Pedido",
            Content = $"¿Generar una venta documental desde el Pedido #{ViewModel.DocumentoId}? Se copiarán cliente, productos y observaciones.",
            PrimaryButtonText = "Generar", SecondaryButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await ViewModel.GenerarVentaCommand.ExecuteAsync(null);
    }

    private async void BtnAgregarLinea_Click(object sender, RoutedEventArgs e)
    {
        var detalle = await MostrarDialogoNuevaLinea();
        if (detalle is not null)
            await ViewModel.AgregarDetalleLocalAsync(detalle);
    }

    private async void BtnGenerarOT_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeGenerarOT) { ViewModel.ErrorMessage = "El pedido debe estar Confirmado o En Proceso para generar una OT."; return; }
        var txtDescripcion = new TextBox { PlaceholderText = "Descripcion del trabajo a realizar", AcceptsReturn = true };
        var dialog = new ContentDialog
        {
            Title = "Generar Orden de Trabajo", PrimaryButtonText = "Generar", SecondaryButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot,
            Content = new StackPanel { Spacing = 8, Children = {
                new TextBlock { Text = "Descripcion del trabajo *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                txtDescripcion } }
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (string.IsNullOrWhiteSpace(txtDescripcion.Text)) { ViewModel.ErrorMessage = "La descripcion es obligatoria."; return; }
        await ViewModel.GenerarOrdenTrabajoCommand.ExecuteAsync(txtDescripcion.Text.Trim());
    }

    private async void BtnCancelarPedido_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeCancelar) { ViewModel.ErrorMessage = "Este pedido no se puede cancelar."; return; }
        var dialog = new ContentDialog
        {
            Title = "Confirmar cancelacion", Content = "Cancelar este pedido?",
            PrimaryButtonText = "Si", SecondaryButtonText = "No",
            DefaultButton = ContentDialogButton.Secondary, XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.CancelarAsync();
    }

    /// <summary>
    /// Diálogo institucional para agregar línea al Pedido.
    /// Usa AutoSuggestBox con _productoService.BuscarAsync — mismo selector que Cotizaciones (ADR-040).
    /// ProductoSuggestion wrapper visible en este namespace (definido en CotizacionDocumentoPage.xaml.cs).
    /// </summary>
    private async Task<DetalleLineaEditable?> MostrarDialogoNuevaLinea()
    {
        var asbProducto = new AutoSuggestBox
        {
            PlaceholderText = "Buscar por código o nombre (mínimo 2 caracteres)...",
            QueryIcon       = new SymbolIcon(Symbol.Find)
        };
        var txtDescripcion    = new TextBox { PlaceholderText = "Descripción (se carga al seleccionar producto)" };
        var txtCantidad       = new TextBox { PlaceholderText = "Cantidad", Text = "1" };
        var txtPrecioUnitario = new TextBox { PlaceholderText = "Precio unitario", Text = "0" };
        var txtDescuento      = new TextBox { PlaceholderText = "Descuento % (0 = sin descuento)", Text = "0" };

        var txbInfoProducto = new TextBlock
        {
            Text         = "",
            FontSize     = 11,
            Foreground   = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
        };

        ProductoDto? productoSeleccionado = null;

        asbProducto.TextChanged += async (s, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            var q = s.Text.Trim();
            if (q.Length < 2) { s.ItemsSource = null; return; }
            var resultados = await _productoService.BuscarAsync(_session.EmpresaId, q);
            s.ItemsSource = resultados.Select(p => new ProductoSuggestion(p)).ToList();
        };

        asbProducto.SuggestionChosen += async (s, args) =>
        {
            if (args.SelectedItem is not ProductoSuggestion sugg) return;
            var p = sugg.Producto;
            productoSeleccionado   = p;
            s.Text                 = $"{p.Codigo} — {p.Nombre}";
            txtDescripcion.Text    = p.Nombre;
            txtPrecioUnitario.Text = p.Precio.ToString("F2");

            var existencias     = await _inventarioService.ListarExistenciasAsync(_session.EmpresaId);
            var totalExistencia = existencias.Where(e => e.ProductoId == p.Id).Sum(e => e.Cantidad);

            txbInfoProducto.Text = totalExistencia > 0
                ? $"Existencia disponible: {totalExistencia:N2} {p.UnidadMedidaAbreviatura ?? ""}"
                : "Sin existencia registrada para este producto.";
        };

        var panel = new StackPanel { Spacing = 8, MinWidth = 380 };
        void Lbl(string t) => panel.Children.Add(new TextBlock
        {
            Text = t, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12
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
        Lbl("Descuento % (opcional)");
        panel.Children.Add(txtDescuento);

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
        decimal.TryParse(txtDescuento.Text.Trim(), out var descuentoPct);
        descuentoPct = Math.Clamp(descuentoPct, 0m, 100m);

        return new DetalleLineaEditable(
            id:             0,
            productoId:     productoSeleccionado.Id,
            descripcion:    desc,
            cantidad:       qty,
            precioUnitario: precio,
            sku:            productoSeleccionado.Codigo,
            ivaAplicable:   productoSeleccionado.IvaAplicable,
            descuentoPct:   descuentoPct);
    }

    // ── Dimensión financiera — Anticipos (ADR-065) ────────────────────────────

    private async void BtnRegistrarAnticipo_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeRegistrarAnticipo) return;

        var txtMonto = new NumberBox
        {
            PlaceholderText = "0.00",
            Minimum = 0.01,
            SmallChange = 100,
            LargeChange = 1000,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var cmbFormaPago = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedIndex = 0,
            Items = { "Efectivo", "Transferencia", "Tarjeta", "Cheque", "Otro" }
        };
        var txtReferencia = new TextBox { PlaceholderText = "Número de transferencia, referencia, etc. (opcional)" };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Monto *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(txtMonto);
        panel.Children.Add(new TextBlock { Text = "Forma de Pago *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(cmbFormaPago);
        panel.Children.Add(new TextBlock { Text = "Referencia" });
        panel.Children.Add(txtReferencia);

        var dialog = new ContentDialog
        {
            Title = "Registrar Anticipo",
            Content = panel,
            PrimaryButtonText = "Registrar",
            SecondaryButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var monto = (decimal)txtMonto.Value;
        if (monto <= 0) { ViewModel.ErrorMessage = "El monto debe ser mayor a cero."; return; }

        var formaPago = cmbFormaPago.SelectedItem?.ToString() ?? "Efectivo";
        var referencia = string.IsNullOrWhiteSpace(txtReferencia.Text) ? null : txtReferencia.Text.Trim();

        await ViewModel.RegistrarAnticipoAsync(monto, formaPago, referencia);
    }
}

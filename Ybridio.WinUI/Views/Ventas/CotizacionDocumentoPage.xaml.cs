using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.Application.DTOs.Directorio;
using Ybridio.Application.DTOs.Inventario;
using Ybridio.Application.Services.Configuracion;
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
    private readonly IWindowManager            _windowManager;
    private readonly IDirectorioService        _directorioService;     // Hydration del selector
    private readonly IOtroCargoService         _otroCargoService;      // Cargos accesorios
    private readonly CotizacionDto?            _cotizacionOriginal;    // ADR-039

    public CotizacionDocumentoViewModel ViewModel { get; }

    public CotizacionDocumentoPage(CotizacionDto? cotizacion)
    {
        ViewModel = new CotizacionDocumentoViewModel(
            App.Services.GetRequiredService<ICotizacionService>(),
            App.Services.GetRequiredService<IRelacionComercialService>(),
            App.Services.GetRequiredService<Ybridio.Application.Services.Autorizacion.IErpAuthorizationService>(),
            App.Services.GetRequiredService<SessionService>(),
            App.Services.GetRequiredService<Ybridio.WinUI.Services.Diagnostic.IOperationalObservabilityService>(),
            App.Services.GetRequiredService<Ybridio.WinUI.Services.Diagnostic.ICurrentContextTracker>(),
            App.Services.GetRequiredService<Ybridio.Application.Services.Configuracion.IConfiguracionFiscalService>());

        _workspace          = App.Services.GetRequiredService<IWorkspaceService>();
        _productoService    = App.Services.GetRequiredService<IProductoService>();
        _inventarioService  = App.Services.GetRequiredService<IInventarioService>();
        _session            = App.Services.GetRequiredService<SessionService>();
        _windowManager      = App.Services.GetRequiredService<IWindowManager>();
        _directorioService  = App.Services.GetRequiredService<IDirectorioService>();
        _otroCargoService   = App.Services.GetRequiredService<IOtroCargoService>();
        _cotizacionOriginal = cotizacion;

        _hidratandoUI = true;
        InitializeComponent();

        ViewModel.NotificarPedidoGenerado = AbrirPedidoEnWorkspace;
        ViewModel.SolicitarAgregarCargo   = AbrirDialogoAgregarCargo;
        ViewModel.Initialize(cotizacion);

        // Mostrar chip inmediatamente con el DTO sintético (solo nombre).
        // La hidratación real (tipo correcto, email, teléfono) se carga de forma async
        // mediante HidratarSelectorClienteAsync para no bloquear el constructor.
        if (ViewModel.EntidadDirectorioSeleccionada is not null)
            SelectorCliente.EntidadSeleccionada = ViewModel.EntidadDirectorioSeleccionada;

        // Selector DTO Hydration: carga asíncrona del DTO completo desde BD.
        // Reemplaza el DTO sintético mínimo (tipo=Empresa, sin email/teléfono) por la
        // entidad real con todos los datos correctos. Best-effort — no bloquea la UI.
        if (cotizacion?.RelacionComercialId is int relacionId)
            _ = HidratarSelectorClienteAsync(relacionId);

        // Commercial Tax Pattern: carga la tasa IVA desde IConfiguracionFiscalService.
        // Fire-and-forget — el fallback (FiscalConstants.TasaIvaEstandar) está activo hasta que complete.
        _ = ViewModel.CargarConfiguracionFiscalAsync();

        _hidratandoUI = false;
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
        _directorioService  = App.Services.GetRequiredService<IDirectorioService>();
        _cotizacionOriginal = cotizacionOriginal;

        _hidratandoUI = true;
        InitializeComponent();

        // Rehost: el ViewModel ya tiene EntidadDirectorioSeleccionada completamente hidratada
        // desde la página original — no hay que re-cargar desde BD.
        if (ViewModel.EntidadDirectorioSeleccionada is not null)
            SelectorCliente.EntidadSeleccionada = ViewModel.EntidadDirectorioSeleccionada;

        ViewModel.NotificarPedidoGenerado = AbrirPedidoEnWorkspace;
        _hidratandoUI = false;
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
            // Separador + botón workspace: ambos visibles juntos (definen la superficie de comando)
            SepAbrirVentana.Visibility   = vis;
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
    /// </summary>
    private void BtnAbrirEnVentana_Click(object sender, RoutedEventArgs e)
    {
        var titulo = !string.IsNullOrWhiteSpace(ViewModel.NombreCliente)
            ? $"Cotización - {ViewModel.NombreCliente}"
            : (ViewModel.IsNuevo ? "Nueva Cotización" : $"Cotización #{_cotizacionOriginal?.Id}");

        // Global Document Runtime Ownership Pattern:
        // ViewModel.DocumentoId se actualiza tras el primer guardado (inicialmente null).
        // _cotizacionOriginal?.Id permanece null para documentos creados nuevos aunque ya estén guardados.
        // Usar DocumentoId garantiza que TryActivateWindow encuentre la ventana con el ID real de BD.
        var cotizacionId = ViewModel.DocumentoId?.ToString() ?? _sessionKey;
        var windowKey    = $"detached:cotizacion:{cotizacionId}";

        var viewModelActual    = ViewModel;
        var cotizacionOriginal = _cotizacionOriginal;

        try
        {
            VolverALista?.Invoke();

            _windowManager.OpenWindow<DetachedDocumentWindow, string>(
                key: windowKey,
                factory: () =>
                {
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

    // Guard que impide que la restauración de DescuentoGlobalPct al cargar la página
    // dispare la alerta de confirmación de descuento global (ADR-043).
    // Se activa antes de que los bindings XAML hidraten los controles y se limpia en Loaded.
    private bool _hidratandoUI;

    // ── Protección de cierre (ADR-040) ────────────────────────────────────────

    /// <summary>
    /// Muestra el diálogo institucional de confirmación al cerrar un documento con cambios no guardados.
    /// Devuelve <c>true</c> si el usuario decidió guardar o descartar; <c>false</c> si canceló.
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
            await ViewModel.GuardarCommand.ExecuteAsync(null);
            return true;
        }
        if (result == ContentDialogResult.Secondary)
            return true;

        return false;
    }

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

    // ── Handlers: selector de cliente (ADR-038) ────────────────────────────

    private void SelectorCliente_SelectionChanged(object? sender, DirectorioSelectorDto? entidad)
    {
        if (entidad is not null)
            ViewModel.SeleccionarCliente(entidad);
        else
            ViewModel.LimpiarCliente();
    }

    /// <summary>
    /// Hidratación asíncrona del selector de cliente (Selector DTO Hydration Rule).
    ///
    /// <para>El constructor de Initialize() crea un DTO sintético mínimo con solo el nombre,
    /// EntityType=Empresa hardcodeado y sin email/teléfono. Este método lo reemplaza con el
    /// DTO real cargado desde BD que tiene: EntityType correcto (Persona o Empresa),
    /// email, teléfono y RFC completos.</para>
    ///
    /// <para>Best-effort: si falla (red, entidad eliminada) el chip muestra solo el nombre.
    /// No bloquea la UI — se llama fire-and-forget desde el constructor.</para>
    /// </summary>
    private async Task HidratarSelectorClienteAsync(int relacionComercialId)
    {
        try
        {
            var dto = await _directorioService.ObtenerDtoParaSelectorAsync(
                relacionComercialId, CancellationToken.None);

            if (dto is null) return;

            // Restaurar en ViewModel (sin marcar dirty) y en el selector visual
            ViewModel.RestaurarEntidadSeleccionada(dto);
            SelectorCliente.EntidadSeleccionada = dto;
        }
        catch
        {
            // Hydration best-effort: el chip muestra solo el nombre del DTO sintético
        }
    }


    // ── Handlers de CommandBar ────────────────────────────────────────────────

    /// <summary>
    /// Enviar — ACCIÓN OPERACIONAL (Commercial Document Workflow Pattern).
    /// NO modifica el estado comercial de la cotización.
    /// Una cotización Aprobada puede enviarse múltiples veces.
    /// V1: stub informativo. Future: generar PDF, enviar correo, registrar auditoría de envío.
    /// </summary>
    private void BtnEnviar_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeEnviar) return;
        ViewModel.SuccessMessage = "Cotización lista para envío. (PDF/correo disponible próximamente)";
    }

    /// <summary>
    /// Aprobar — transición de workflow Borrador → Aprobada.
    /// </summary>
    private async void BtnAprobar_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeAprobar) { ViewModel.ErrorMessage = "Solo se puede aprobar una cotización en estado Borrador."; return; }
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

    // ── Handlers: campos de fecha (Operational Date Display Pattern) ─────────

    /// <summary>
    /// El usuario seleccionó una fecha en el CalendarDatePicker.
    /// Actualiza ViewModel.FechaOffset → INPC notifica a LblFecha via x:Bind OneWay.
    /// </summary>
    private void DpFecha_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (args.NewDate.HasValue)
            ViewModel.FechaOffset = args.NewDate.Value;
    }

    /// <summary>
    /// El usuario seleccionó una vigencia en el CalendarDatePicker.
    /// Actualiza ViewModel.FechaVigenciaOffset → INPC notifica a LblVigencia via x:Bind OneWay.
    /// </summary>
    private void DpVigencia_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (args.NewDate.HasValue)
            ViewModel.FechaVigenciaOffset = args.NewDate.Value;
    }

    // ── Handlers de edición inline ────────────────────────────────────────────

    /// <summary>
    /// Edición inline de cantidad (ADR-041).
    /// Para documentos NUEVOS: el binding TwoWay ya aplica el cambio y recalcula vía INPC.
    /// Para documentos EXISTENTES: persiste el detalle en BD via ActualizarCantidadAsync.
    /// Cantidad = 0: solicita confirmación antes de eliminar la línea.
    /// </summary>
    private async void NumberBox_Cantidad_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue)) return;
        if (args.NewValue == args.OldValue) return;
        if (sender.Tag is not DetalleLineaEditable linea) return;

        var nuevaCantidad = (decimal)args.NewValue;

        // Cantidad 0: confirmar eliminación antes de proceder
        if (nuevaCantidad == 0)
        {
            var confirmar = await MostrarConfirmacionEliminarLineaAsync(linea.Descripcion);
            if (!confirmar)
            {
                // Restaurar valor anterior sin disparar el handler
                sender.ValueChanged -= NumberBox_Cantidad_ValueChanged;
                sender.Value = (double)linea.Cantidad;
                sender.ValueChanged += NumberBox_Cantidad_ValueChanged;
                return;
            }
        }

        await ViewModel.ActualizarCantidadAsync(linea, nuevaCantidad);
    }

    /// <summary>
    /// Edición inline de descuento por línea (ADR-042 / ADR-043).
    /// Para documentos NUEVOS: el binding TwoWay ya aplica el cambio y recalcula vía INPC.
    /// Para documentos EXISTENTES: persiste el cambio via ActualizarDescuentoAsync (delete+readd).
    /// </summary>
    /// <remarks>
    /// Guard IsBusy (ADR-043, defensa en profundidad): rechaza llamadas al servicio mientras
    /// el ViewModel está procesando otra operación EF. Esto evita la segunda ruta de concurrencia
    /// DbContext cuando el usuario edita otra línea mientras AplicarDescuentoGlobalALineas (Fase 2)
    /// ya tiene IsBusy = true.
    ///
    /// El guard anti-reentrancy en <see cref="CotizacionDocumentoViewModel.ActualizarDescuentoAsync"/>
    /// es la protección primaria (detecta valor ya aplicado). Este guard es defensa secundaria.
    /// </remarks>
    private async void NumberBox_Descuento_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue)) return;
        if (args.NewValue == args.OldValue) return;
        if (sender.Tag is not DetalleLineaEditable linea) return;
        if (ViewModel.IsBusy) return;

        var nuevoPct = Math.Clamp((decimal)args.NewValue, 0m, 100m);

        // Global Discount Lifecycle (ADR-042): si el usuario cambia manualmente una línea
        // a un valor distinto del global, ya no existe uniformidad — invalidar silenciosamente.
        // NO elimina descuentos de línea. NO muestra alertas. Solo borra el indicador global.
        if (nuevoPct != ViewModel.DescuentoGlobalPct)
            ViewModel.InvalidarDescuentoGlobal();

        // Solo persistir en BD para documentos EXISTENTES; para NUEVOS el binding TwoWay ya actualizó
        if (!ViewModel.IsNuevo)
            await ViewModel.ActualizarDescuentoAsync(linea, nuevoPct);
    }

    /// <summary>
    /// Descuento global del documento (ADR-042 / ADR-043).
    /// Muestra confirmación solo cuando el usuario aplica activamente un NUEVO descuento global
    /// y existen descuentos individuales en líneas.
    /// No muestra alerta al abrir, editar, rehidratar o detach/attach del documento.
    /// </summary>
    private async void NbDescuentoGlobal_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue)) return;
        if (args.NewValue == args.OldValue) return;

        // Guard 1 — hidratación síncrona: protege el ciclo de InitializeComponent().
        if (_hidratandoUI) return;

        // Guard 2 — actualizaciones programáticas diferidas de x:Bind OneWay (causa raíz del bug).
        //
        // Cuando ViewModel.Initialize / DetectarDescuentoGlobal establece DescuentoGlobalPct,
        // el binding x:Bind (Mode=OneWay) despacha la actualización del NumberBox en el ciclo
        // siguiente del DispatcherQueue — DESPUÉS de que _hidratandoUI ya es false.
        // En ese momento, args.NewValue ya coincide exactamente con ViewModel.DescuentoGlobalPct
        // porque el ViewModel fue quien lo generó. No es un gesto del usuario → sin alerta.
        //
        // También actúa como guard de re-entrada cuando AplicarDescuentoGlobalALineas establece
        // DescuentoGlobalPct en Fase 1 y x:Bind refleja el valor de vuelta al NumberBox.
        if ((decimal)args.NewValue == ViewModel.DescuentoGlobalPct) return;

        // Guard 3 — IsBusy: no iniciar operación global si servicio ya ocupado.
        if (ViewModel.IsBusy) return;

        var pct = (decimal)args.NewValue;

        // No acumulable: si existen descuentos en líneas y el usuario aplica uno global, confirmar
        if (pct > 0 && ViewModel.HayDescuentosEnLineas)
        {
            var continuar = await MostrarConfirmacionDescuentoGlobalAsync();
            if (!continuar)
            {
                // Restaurar valor anterior sin disparar el handler
                sender.ValueChanged -= NbDescuentoGlobal_ValueChanged;
                sender.Value = (double)ViewModel.DescuentoGlobalPct;
                sender.ValueChanged += NbDescuentoGlobal_ValueChanged;
                return;
            }
        }

        if (pct <= 0)
            await ViewModel.LimpiarDescuentoGlobal();
        else
            await ViewModel.AplicarDescuentoGlobalALineas(pct);
    }

    /// <summary>
    /// Diálogo institucional de confirmación para descuento global sobre líneas con descuento individual.
    /// Regla ADR-042: descuento global NO acumulable con descuentos por línea.
    /// </summary>
    private async Task<bool> MostrarConfirmacionDescuentoGlobalAsync()
    {
        var dialog = new ContentDialog
        {
            Title               = "Descuento global",
            Content             = "Aplicar descuento global eliminará los descuentos individuales por línea. ¿Desea continuar?",
            PrimaryButtonText   = "Continuar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Secondary,
            XamlRoot            = XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Botón eliminar por línea — confirma antes de eliminar (ADR-041).
    /// </summary>
    private async void BtnEliminarLinea_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DetalleLineaEditable linea) return;
        var confirmar = await MostrarConfirmacionEliminarLineaAsync(linea.Descripcion);
        if (!confirmar) return;
        ViewModel.DetalleSeleccionado = linea;
        await ViewModel.EliminarDetalleCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Confirmación institucional para eliminar una línea (por botón o por cantidad = 0).
    /// </summary>
    private async Task<bool> MostrarConfirmacionEliminarLineaAsync(string descripcion)
    {
        var dialog = new ContentDialog
        {
            Title               = "Eliminar línea",
            Content             = $"¿Eliminar \"{descripcion}\" de la cotización?",
            PrimaryButtonText   = "Eliminar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Secondary,
            XamlRoot            = XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void BtnAgregarLinea_Click(object sender, RoutedEventArgs e)
    {
        var detalle = await MostrarDialogoNuevaLinea();
        if (detalle is null) return;
        // ADR-040: merge-or-add — nunca crear líneas duplicadas del mismo producto
        await ViewModel.AgregarOIncrementarDetalleAsync(detalle);

        // Global Discount Lifecycle: si la línea tiene un descuento individual distinto del global,
        // ya no existe uniformidad — invalidar silenciosamente sin alerta.
        if (detalle.DescuentoPct != ViewModel.DescuentoGlobalPct)
            ViewModel.InvalidarDescuentoGlobal();
    }

    // ── Diálogo: Agregar Línea con Producto real ──────────────────────────────

    private async System.Threading.Tasks.Task<DetalleLineaEditable?> MostrarDialogoNuevaLinea()
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

        // AutoSuggestBox muestra los ítems del dropdown usando .ToString() de cada objeto.
        // ProductoDto es un sealed record cuyo ToString() devuelve la representación técnica del record.
        // Usamos un wrapper local que override ToString() para mostrar "Codigo — Nombre" en el dropdown.
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
            id:           0,
            productoId:   productoSeleccionado.Id,
            descripcion:  desc,
            cantidad:     qty,
            precioUnitario: precio,
            sku:          productoSeleccionado.Codigo,
            ivaAplicable: productoSeleccionado.IvaAplicable,
            descuentoPct: descuentoPct);
    }

    // ── Otros Cargos — Commercial Charges Pattern ─────────────────────────────

    private void BtnAgregarCargo_Click(object sender, RoutedEventArgs e)
        => _ = ViewModel.AgregarCargoAsync();

    private void BtnEliminarCargo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CargoLineaEditable cargo)
        {
            ViewModel.CargoSeleccionado = cargo;
            _ = ViewModel.EliminarCargoAsync();
        }
    }

    /// <summary>
    /// Diálogo para agregar un cargo accesorio (Flete, Maniobras, Seguro, etc.).
    /// Permite seleccionar desde el catálogo OtroCargo o ingresar texto libre.
    /// </summary>
    private async Task<CargoLineaEditable?> AbrirDialogoAgregarCargo()
    {
        // Cargar catálogo de Otros Cargos disponibles
        IReadOnlyList<OtroCargoDto> catalogoCargos;
        try { catalogoCargos = await _otroCargoService.ListarAsync(); }
        catch { catalogoCargos = []; }

        // ComboBox para seleccionar del catálogo (opcional)
        var cmbCatalogo = new ComboBox { PlaceholderText = "Seleccionar del catálogo (opcional)", MinWidth = 300 };
        cmbCatalogo.Items.Add(new ComboBoxItem { Content = "(Cargo libre — ingresar descripción manual)", Tag = (OtroCargoDto?)null });
        foreach (var c in catalogoCargos)
            cmbCatalogo.Items.Add(new ComboBoxItem { Content = $"{c.Nombre}", Tag = c });
        cmbCatalogo.SelectedIndex = 0;

        var txtDescripcion = new TextBox { PlaceholderText = "Descripción del cargo" };
        var nbImporte      = new NumberBox
        {
            Value                   = 0,
            Minimum                 = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            NumberFormatter         = new Windows.Globalization.NumberFormatting.CurrencyFormatter("MXN") { FractionDigits = 2 }
        };
        var chkAplicaIva   = new CheckBox { Content = "Aplica IVA", IsChecked = false };

        // Auto-llenar desde catálogo al seleccionar
        cmbCatalogo.SelectionChanged += (_, _) =>
        {
            if (cmbCatalogo.SelectedItem is ComboBoxItem ci && ci.Tag is OtroCargoDto cargo)
            {
                txtDescripcion.Text    = cargo.Nombre;
                chkAplicaIva.IsChecked = cargo.AplicaIva;
            }
        };

        var panel = new StackPanel { Spacing = 10, MinWidth = 340 };
        panel.Children.Add(new TextBlock { Text = "Cargo del catálogo", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(cmbCatalogo);
        panel.Children.Add(new TextBlock { Text = "Descripción *", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(txtDescripcion);
        panel.Children.Add(new TextBlock { Text = "Importe ($)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(nbImporte);
        panel.Children.Add(chkAplicaIva);

        var dialog = new ContentDialog
        {
            Title               = "Agregar Cargo Accesorio",
            PrimaryButtonText   = "Agregar",
            SecondaryButtonText = "Cancelar",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = XamlRoot,
            Content             = panel
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
        if (string.IsNullOrWhiteSpace(txtDescripcion.Text))
        {
            ViewModel.ErrorMessage = "La descripción del cargo es requerida.";
            return null;
        }

        int? otroCargoId = null;
        if (cmbCatalogo.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is OtroCargoDto selectedCargo)
            otroCargoId = selectedCargo.Id;

        return new CargoLineaEditable(
            id:          0,
            otroCargoId: otroCargoId,
            descripcion: txtDescripcion.Text.Trim(),
            importe:     (decimal)nbImporte.Value,
            aplicaIva:   chkAplicaIva.IsChecked ?? false);
    }
}

/// <summary>
/// Wrapper de ProductoDto para el AutoSuggestBox del diálogo de agregar línea.
/// AutoSuggestBox llama .ToString() en cada ítem para mostrar el dropdown.
/// ProductoDto es un sealed record con ToString() técnico ("ProductoDto { Id=1, ... }").
/// Este wrapper devuelve el formato visual operacional: "Codigo — Nombre".
/// </summary>
internal sealed class ProductoSuggestion
{
    public ProductoDto Producto { get; }

    public ProductoSuggestion(ProductoDto producto) => Producto = producto;

    public override string ToString() => $"{Producto.Codigo} — {Producto.Nombre}";
}

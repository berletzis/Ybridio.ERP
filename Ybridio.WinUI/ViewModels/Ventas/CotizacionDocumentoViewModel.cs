using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Directorio;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Application.Services.Configuracion;
using Ybridio.Application.Services.Directorio;
using Ybridio.Application.Services.Venta;
using Ybridio.Domain.Common;
using Ybridio.Domain.Ventas;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Diagnostic;

namespace Ybridio.WinUI.ViewModels.Ventas;

/// <summary>
/// ViewModel de documento para el formulario de cotización (nueva o existente).
/// Gestiona el encabezado y las líneas de detalle de la cotización.
///
/// <para>Para documentos NUEVOS: los detalles se acumulan en memoria hasta "Guardar"
/// que llama a CrearAsync con todos los datos en una sola operación.</para>
///
/// <para>Para documentos EXISTENTES: agregar/quitar detalles es inmediato (persiste
/// al servicio). El encabezado se actualiza con "Guardar" via ActualizarAsync.</para>
/// </summary>
/// <remarks>
/// Fórmulas (ADR-040 + ADR-042 — Commercial Discount Pattern):
/// - DetalleLinea.Importe = Cantidad × PrecioUnitario × (1 − DescuentoPct/100)
///   calculado por <see cref="CommercialDocumentCalculator.CalcularImporteLinea"/>.
/// - SubtotalBruto = SUM(Cantidad × PrecioUnitario) — sin descuento
/// - Subtotal      = SUM(Detalles.Importe)          — neto con descuento
/// - DescuentoTotal = SubtotalBruto − Subtotal
/// - Impuestos     = SUM(Importe líneas con IVA) × TasaIvaEstandar
/// - Total         = Subtotal + Impuestos
/// </remarks>
public sealed partial class CotizacionDocumentoViewModel : ObservableObject
{
    private readonly ICotizacionService               _service;
    private readonly IRelacionComercialService         _relacionComercialService;
    private readonly IErpAuthorizationService         _auth;
    private readonly SessionService                   _session;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;
    private readonly IConfiguracionFiscalService      _configuracionFiscal;

    /// <summary>
    /// Tasa IVA activa (0..1) cargada desde IConfiguracionFiscalService.
    /// Fallback: FiscalConstants.TasaIvaEstandar hasta que CargarConfiguracionFiscalAsync complete.
    /// </summary>
    private decimal _tasaIva = FiscalConstants.TasaIvaEstandar;

    // El documento cargado (null si es nuevo)
    private CotizacionDto? _documento;

    /// <summary>
    /// ID del documento cotización actualmente cargado.
    /// Null si el documento es nuevo y no ha sido guardado todavía.
    /// Expuesto para que la Page pueda construir correctamente la window key
    /// del Single Document Session Rule (Global Document Runtime Ownership Pattern).
    /// </summary>
    public long? DocumentoId => _documento?.Id;
    // Entidad de Directorio seleccionada (ADR-038). RelacionComercialId se resuelve en GuardarAsync.
    private DirectorioSelectorDto? _entidadDirectorioSeleccionada;
    // True solo cuando el usuario cambió activamente el cliente; false al inicializar/rehidratar.
    // Impide llamar GetOrCreate innecesariamente al guardar documentos existentes sin cambio de cliente.
    private bool _clienteModificadoPorUsuario;

    /// <summary>
    /// Entidad del Directorio actualmente seleccionada (ADR-038 / ADR-039).
    /// Expuesto públicamente para que la página de rehost pueda restaurar el chip del selector
    /// sin llamar a Initialize() (Shared ViewModel Pattern).
    /// </summary>
    public DirectorioSelectorDto? EntidadDirectorioSeleccionada => _entidadDirectorioSeleccionada;

    // ── Estado del documento ─────────────────────────────────────────────────
    [ObservableProperty] private bool              isNuevo = true;
    [ObservableProperty] private bool              isBusy;
    [ObservableProperty] private string            errorMessage   = string.Empty;
    [ObservableProperty] private string            successMessage = string.Empty;

    // ── Encabezado (editable) ────────────────────────────────────────────────
    [ObservableProperty] private string            nombreCliente  = string.Empty;
    [ObservableProperty] private int?              _relacionComercialId;
    [ObservableProperty] private DateTime          fecha          = DateTime.Today;
    [ObservableProperty] private DateTime?         fechaVigencia;

    /// <summary>Wrapper DateTimeOffset para el DatePicker de Fecha.</summary>
    public DateTimeOffset FechaOffset
    {
        get => new DateTimeOffset(Fecha);
        set => Fecha = value.DateTime;
    }

    /// <summary>Wrapper DateTimeOffset? para el DatePicker de FechaVigencia.</summary>
    public DateTimeOffset FechaVigenciaOffset
    {
        get => FechaVigencia.HasValue ? new DateTimeOffset(FechaVigencia.Value) : DateTimeOffset.Now.AddDays(30);
        set => FechaVigencia = value.DateTime;
    }

    partial void OnFechaChanged(DateTime value)          => OnPropertyChanged(nameof(FechaOffset));
    partial void OnFechaVigenciaChanged(DateTime? value) => OnPropertyChanged(nameof(FechaVigenciaOffset));
    partial void OnObservacionesChanged(string? value)   => IsDirty = true;
    [ObservableProperty] private string?           observaciones;
    [ObservableProperty] private EstatusCotizacion estatus        = EstatusCotizacion.Borrador;

    // ── Info de cliente seleccionado (solo lectura, cargado al seleccionar) ──
    [ObservableProperty] private string? clienteEmail;
    [ObservableProperty] private string? clienteTelefono;
    [ObservableProperty] private decimal clienteLimiteCredito;

    /// <summary>True cuando hay un cliente seleccionado del Directorio o un RelacionComercialId existente (ADR-038).</summary>
    public bool TieneClienteSeleccionado => _entidadDirectorioSeleccionada is not null || RelacionComercialId.HasValue;

    /// <summary>ID de empresa del contexto de sesión, expuesto para binding en el selector control.</summary>
    public int EmpresaId => _session.EmpresaId;

    // ── Detalles ─────────────────────────────────────────────────────────────
    public ObservableCollection<DetalleLineaEditable> Detalles { get; } = [];

    // ── Totales (runtime, recalculados cuando cambian los detalles) ─────────

    /// <summary>
    /// Subtotal bruto antes de descuentos: SUM(Cantidad × PrecioUnitario).
    /// Visible solo cuando hay descuentos activos.
    /// </summary>
    [ObservableProperty] private decimal subtotalBruto;

    /// <summary>
    /// Subtotal neto después de descuentos: SUM(Detalles.Importe).
    /// Calculado runtime; se persiste en BD al llamar Guardar.
    /// </summary>
    [ObservableProperty] private decimal subtotal;

    /// <summary>
    /// Monto monetario total de descuentos: SubtotalBruto − Subtotal.
    /// Visible en totales solo cuando HayDescuento es true.
    /// </summary>
    [ObservableProperty] private decimal descuentoTotal;

    /// <summary>
    /// Total = Subtotal + OtrosCargos + Impuestos (ADR-040 + Commercial Charges Pattern).
    /// Se persistirá en BD al llamar Guardar.
    /// </summary>
    [ObservableProperty] private decimal total;

    /// <summary>
    /// Suma de todos los cargos accesorios (Flete, Maniobras, Seguro, etc.) del documento.
    /// Commercial Charges Pattern: sección separada de las líneas de producto.
    /// </summary>
    [ObservableProperty] private decimal totalOtrosCargos;

    /// <summary>Hay al menos un cargo accesorio en el documento.</summary>
    public bool HayCargos => Cargos.Count > 0;

    /// <summary>
    /// Impuestos = SUM(líneas con IVA) × TasaIvaEstandar (ADR-040).
    /// Calculado runtime; no persiste de forma independiente (el Total ya lo incluye).
    /// </summary>
    [ObservableProperty] private decimal impuestos;

    /// <summary>
    /// Porcentaje de descuento global aplicado al documento (0–100).
    /// Cuando se aplica, reemplaza todos los descuentos individuales (ADR-042).
    /// Runtime — no persiste directamente; se refleja en DescuentoPct de cada línea.
    /// </summary>
    [ObservableProperty] private decimal descuentoGlobalPct;

    /// <summary>Wrapper double de DescuentoGlobalPct para el NumberBox de WinUI.</summary>
    public double DescuentoGlobalPctDouble => (double)DescuentoGlobalPct;

    partial void OnDescuentoGlobalPctChanged(decimal value)
        => OnPropertyChanged(nameof(DescuentoGlobalPctDouble));

    /// <summary>Hay al menos un descuento activo en el documento (línea o global).</summary>
    public bool HayDescuento => DescuentoTotal > 0;

    /// <summary>Hay al menos una línea con DescuentoPct > 0.</summary>
    public bool HayDescuentosEnLineas => Detalles.Any(d => d.DescuentoPct > 0);

    /// <summary>
    /// Indica si el documento tiene cambios no guardados (ADR-040).
    /// Se activa al agregar/eliminar líneas, cambiar cantidades/descuentos, cambiar cliente u observaciones.
    /// Se reinicia a <c>false</c> tras guardar exitosamente.
    /// </summary>
    [ObservableProperty] private bool isDirty;

    /// <summary>
    /// Total de artículos = SUM(Cantidad) de todas las líneas. Status bar info (ADR-041).
    /// </summary>
    [ObservableProperty] private decimal totalArticulos;

    // ── UI helpers ───────────────────────────────────────────────────────────
    public string TituloDocumento => IsNuevo
        ? "Nueva Cotización"
        : !string.IsNullOrEmpty(_documento?.Folio)
            ? $"Cotización {_documento.Folio}"
            : $"Cotización #{_documento?.Id}";
    /// <summary>
    /// Texto del badge de estado comercial — Commercial Document Workflow Pattern.
    /// "Enviada" (legacy) se muestra como "Aprobada" en el nuevo modelo.
    /// </summary>
    public string EstatusTextoDisplay => Estatus switch
    {
        EstatusCotizacion.Borrador   => "Borrador",
#pragma warning disable CS0618
        EstatusCotizacion.Enviada    => "Aprobada",   // Legacy — tratar como Aprobada en display
#pragma warning restore CS0618
        EstatusCotizacion.Aprobada   => "Aprobada",
        EstatusCotizacion.Convertida => "Convertida",
        EstatusCotizacion.Cancelada  => "Cancelada",
        _                            => Estatus.ToString()
    };

    // ── Guardas del workflow (Commercial Document Workflow Pattern) ──────────

    /// <summary>Puede editar encabezado y cargos: solo en Borrador (Aprobada está congelada).</summary>
    public bool PuedeEditar => Estatus is EstatusCotizacion.Borrador;

    /// <summary>Puede editar líneas de detalle (cantidad, descuento): solo en estado Borrador.</summary>
    public bool PuedeEditarLineas => Estatus is EstatusCotizacion.Borrador;

    /// <summary>
    /// Enviar = ACCIÓN OPERACIONAL (no modifica estado).
    /// Disponible desde cualquier estado activo. Future: email, PDF, auditoría de envío.
    /// Una cotización Aprobada puede enviarse múltiples veces sin cambiar estado.
    /// </summary>
    public bool PuedeEnviar => !IsNuevo && Estatus is not (EstatusCotizacion.Cancelada or EstatusCotizacion.Convertida);

    /// <summary>Aprobar = transición de workflow Borrador → Aprobada.</summary>
#pragma warning disable CS0618
    public bool PuedeAprobar => !IsNuevo && Estatus is EstatusCotizacion.Borrador or EstatusCotizacion.Enviada;
#pragma warning restore CS0618

    /// <summary>Convertir = workflow Aprobada → Convertida (genera Pedido).</summary>
#pragma warning disable CS0618
    public bool PuedeConvertir => !IsNuevo && Estatus is EstatusCotizacion.Aprobada or EstatusCotizacion.Enviada;
#pragma warning restore CS0618

    /// <summary>Cancelar = transición a estado terminal Cancelada (desde cualquier estado activo).</summary>
    public bool PuedeCancelar => !IsNuevo && Estatus is not (EstatusCotizacion.Cancelada or EstatusCotizacion.Convertida);

    /// <summary>Muestra el bloque operaciones cuando la cotización está Aprobada o Convertida.</summary>
#pragma warning disable CS0618
    public bool MostrarOperacionesRealizadas =>
        !IsNuevo && Estatus is EstatusCotizacion.Aprobada or EstatusCotizacion.Convertida;

    /// <summary>Texto resumen de la operación principal de la cotización.</summary>
    public string ResumenOperacion => Estatus switch
    {
        EstatusCotizacion.Aprobada   => "Cotización aprobada comercialmente.",
        EstatusCotizacion.Convertida => "Cotización convertida a Pedido.",
        _                            => string.Empty
    };
#pragma warning restore CS0618

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EliminarDetalleCommand))]
    private DetalleLineaEditable? detalleSeleccionado;

    // ── Otros Cargos (Commercial Charges Pattern) ─────────────────────────────
    /// <summary>Cargos accesorios del documento (Flete, Maniobras, Seguro, etc.).</summary>
    public System.Collections.ObjectModel.ObservableCollection<CargoLineaEditable> Cargos { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EliminarCargoCommand))]
    private CargoLineaEditable? cargoSeleccionado;

    /// <summary>Callback para abrir el diálogo de agregar cargo (requiere XamlRoot).</summary>
    public Func<Task<CargoLineaEditable?>>? SolicitarAgregarCargo;

    // Callbacks para acciones que requieren XamlRoot (abrirán dialogs o workspace)
    public Action<DetalleLineaEditable?>? SolicitarNuevoEditar;
    public Func<Task<DetalleLineaEditable?>>? SolicitarAgregarDetalle;
    public Action<PedidoDto>?               NotificarPedidoGenerado;

    /// <summary>
    /// Callback invocado cuando el documento se guarda exitosamente.
    /// Usado por el Document Surface UX Pattern para notificar al módulo padre
    /// que debe cerrar el surface y refrescar el grid.
    /// </summary>
    public Action? DocumentSaved;

    public CotizacionDocumentoViewModel(
        ICotizacionService               service,
        IRelacionComercialService         relacionComercialService,
        IErpAuthorizationService         auth,
        SessionService                   session,
        IOperationalObservabilityService observability,
        ICurrentContextTracker           contextTracker,
        IConfiguracionFiscalService      configuracionFiscal)
    {
        _service                   = service;
        _relacionComercialService  = relacionComercialService;
        _auth                 = auth;
        _session              = session;
        _observability        = observability;
        _contextTracker       = contextTracker;
        _configuracionFiscal  = configuracionFiscal;
        Detalles.CollectionChanged += (_, _) => RecalcularTotales();
    }

    /// <summary>
    /// Carga la tasa IVA institucional desde IConfiguracionFiscalService (Commercial Tax Pattern).
    /// Best-effort — se llama fire-and-forget desde el constructor de la Page.
    /// Si falla, el fallback FiscalConstants.TasaIvaEstandar permanece activo.
    /// </summary>
    public async System.Threading.Tasks.Task CargarConfiguracionFiscalAsync(
        System.Threading.CancellationToken ct = default)
    {
        try
        {
            _tasaIva = await _configuracionFiscal.ObtenerTasaIvaProductoAsync(ct);
            RecalcularTotales();  // Re-aplicar totales con la tasa real
        }
        catch
        {
            // Best-effort: si falla, _tasaIva ya tiene el fallback de FiscalConstants
        }
    }

    /// <summary>Inicializa el ViewModel con una cotización existente o deja en blanco para nueva.</summary>
    public void Initialize(CotizacionDto? cotizacion)
    {
        _documento = cotizacion;
        IsNuevo    = cotizacion is null;

        if (cotizacion is not null)
        {
            NombreCliente       = cotizacion.NombreCliente;
            RelacionComercialId = cotizacion.RelacionComercialId;
            Fecha               = cotizacion.Fecha;
            FechaVigencia       = cotizacion.FechaVigencia;
            Observaciones       = cotizacion.Observaciones;
            Estatus             = cotizacion.Estatus;

            // Sintetizar entidad de Directorio desde datos del documento (ADR-043).
            // Permite que EntidadDirectorioSeleccionada esté disponible para restaurar
            // el chip del selector en modo edición y en detach/rehost sin llamar Initialize().
            // _clienteModificadoPorUsuario = false → GuardarAsync NO llamará GetOrCreate.
            if (RelacionComercialId.HasValue)
            {
                _entidadDirectorioSeleccionada = new DirectorioSelectorDto
                {
                    EntityType         = DirectorioEntityType.Empresa,
                    EmpresaComercialId = RelacionComercialId,
                    DisplayName        = NombreCliente
                };
            }
            _clienteModificadoPorUsuario = false;

            Detalles.Clear();
            foreach (var d in cotizacion.Detalles)
                Detalles.Add(WirarLinea(new DetalleLineaEditable(
                    d.Id, d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario,
                    sku: d.Sku, ivaAplicable: d.IvaAplicable, descuentoPct: d.DescuentoPct)));

            // Cargar cargos accesorios (Commercial Charges Pattern)
            Cargos.Clear();
            if (cotizacion.Cargos is not null)
                foreach (var c in cotizacion.Cargos.OrderBy(c => c.Orden))
                    Cargos.Add(new CargoLineaEditable(c.Id, c.OtroCargoId, c.Descripcion, c.Importe, c.AplicaIva));

            // Detectar si había un descuento global (todas las líneas con mismo %)
            DetectarDescuentoGlobal(cotizacion.Detalles);
        }

        OnPropertyChanged(nameof(TieneClienteSeleccionado));
        RecalcularTotales();
        OnPropertyChanged(nameof(TituloDocumento));
        OnPropertyChanged(nameof(EstatusTextoDisplay));
        RefrescarPermisosUI();
        IsDirty = false;
    }

    /// <summary>
    /// Restaura la entidad del Directorio desde un DTO completamente hidratado (post-Initialize).
    /// A diferencia de <see cref="SeleccionarCliente"/>, NO marca <see cref="IsDirty"/> como true
    /// porque es una operación de restauración, no una acción del usuario.
    /// </summary>
    /// <remarks>
    /// Selector DTO Hydration Rule: llamar después de Initialize() para reemplazar el DTO
    /// sintético mínimo (solo nombre, tipo erróneo) por el DTO real cargado de BD.
    /// Corrige: EntityType erróneo, EmpresaComercialId incorrecto, Email/Teléfono nulos.
    /// </remarks>
    public void RestaurarEntidadSeleccionada(DirectorioSelectorDto entidad)
    {
        _entidadDirectorioSeleccionada = entidad;
        NombreCliente   = entidad.DisplayName;
        ClienteEmail    = entidad.Email;
        ClienteTelefono = entidad.Telefono;
        OnPropertyChanged(nameof(EntidadDirectorioSeleccionada));
        OnPropertyChanged(nameof(TieneClienteSeleccionado));
        // IsDirty intencional: NO se marca — es restauración de estado, no cambio del usuario
    }

    /// <summary>
    /// Selecciona una entidad del Directorio (Persona o EmpresaComercial) desde el selector (ADR-038).
    /// RelacionComercialId se resolverá mediante GetOrCreate al guardar el documento.
    /// </summary>
    public void SeleccionarCliente(DirectorioSelectorDto? entidad)
    {
        _entidadDirectorioSeleccionada = entidad;
        _clienteModificadoPorUsuario   = true;
        NombreCliente        = entidad?.DisplayName ?? string.Empty;
        ClienteEmail         = entidad?.Email;
        ClienteTelefono      = entidad?.Telefono;
        ClienteLimiteCredito = 0;
        IsDirty = true;
        OnPropertyChanged(nameof(TieneClienteSeleccionado));
    }

    /// <summary>Limpia la selección de cliente (cuando el usuario borra el selector).</summary>
    public void LimpiarCliente()
    {
        _entidadDirectorioSeleccionada = null;
        _clienteModificadoPorUsuario   = true;
        RelacionComercialId  = null;
        NombreCliente        = string.Empty;
        ClienteEmail         = null;
        ClienteTelefono      = null;
        ClienteLimiteCredito = 0;
        IsDirty = true;
        OnPropertyChanged(nameof(TieneClienteSeleccionado));
    }

    // ── Comandos ─────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task GuardarAsync(CancellationToken ct = default)
    {
        if (_session.Usuario is null) return;

        // Validaciones básicas
        if (_entidadDirectorioSeleccionada is null && !RelacionComercialId.HasValue)
            { ErrorMessage = "Debe seleccionar un cliente del Directorio."; return; }
        if (string.IsNullOrWhiteSpace(NombreCliente)) { ErrorMessage = "El nombre del cliente es obligatorio."; return; }
        if (IsNuevo && Detalles.Count == 0) { ErrorMessage = "Debe agregar al menos una línea de detalle."; return; }

        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            // ADR-038: GetOrCreate RelacionComercial bajo demanda.
            // Solo si el usuario cambió activamente el cliente (_clienteModificadoPorUsuario);
            // en documentos existentes sin cambio de cliente ya tenemos RelacionComercialId.
            if (_entidadDirectorioSeleccionada is not null && _clienteModificadoPorUsuario)
            {
                var rc = await _relacionComercialService.GetOrCreateAsync(
                    _session.EmpresaId, _entidadDirectorioSeleccionada, _session.Usuario!.Id, ct);
                if (!rc.Success) { ErrorMessage = rc.Error ?? "No se pudo vincular el cliente."; return; }
                RelacionComercialId = rc.Value;
            }
            if (IsNuevo)
            {
                // NEW document: create header + detalles en una sola operación
                var dto = new CrearCotizacionDto(
                    _session.EmpresaId, _session.SucursalId != 0 ? _session.SucursalId : null,
                    RelacionComercialId, NombreCliente, Fecha, FechaVigencia, Observaciones,
                    Detalles.Select(d => new CrearDetalleLineaDto(
                        d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario,
                        d.DescuentoPct, d.IvaAplicable)).ToList());   // IvaAplicable persiste por línea

                var r = await _service.CrearAsync(dto, _session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo crear."; return; }

                // Commercial Charges Persistence Pattern:
                // Los cargos se acumulan en memoria mientras IsNuevo=true.
                // Una vez creado el documento (con Id asignado) los persistimos individualmente.
                var cargosEnMemoria = Cargos.ToList();
                var cotizacionId    = r.Value!.Id;
                var userId          = _session.Usuario.Id;
                foreach (var cargo in cargosEnMemoria)
                {
                    var cargoDto = new Application.DTOs.Ventas.CrearCotizacionCargoDto(
                        cargo.OtroCargoId, cargo.Descripcion, cargo.Importe, cargo.AplicaIva,
                        cargosEnMemoria.IndexOf(cargo));
                    var rc = await _service.AgregarCargoAsync(cotizacionId, cargoDto, userId, ct);
                    if (rc.Success) cargo.Id = rc.Value!.Id;
                }

                // Recargar desde BD en lugar de usar r.Value directamente:
                // r.Value viene de CrearAsync ANTES de persistir los cargos y SIN cargar
                // navegaciones Producto → Sku = null y Cargos = vacío en Initialize().
                // ObtenerConDetallesAsync tiene Include(Detalles.ThenInclude(Producto)) y
                // Include(Cargos), garantizando snapshot correcto post-guardado.
                var reloaded = await _service.ObtenerConDetallesAsync(r.Value!.Id, ct);
                var dtoFinal = reloaded.Success ? reloaded.Value! : r.Value;

                SuccessMessage = !string.IsNullOrEmpty(dtoFinal.Folio)
                    ? $"Cotización {dtoFinal.Folio} creada."
                    : $"Cotización #{dtoFinal.Id} creada.";
                Initialize(dtoFinal);
                IsDirty = false;

                DocumentSaved?.Invoke();
            }
            else
            {
                // EXISTING document: update header only
                var dto = new ActualizarCotizacionDto(RelacionComercialId, NombreCliente, Fecha, FechaVigencia, Observaciones);
                var r   = await _service.ActualizarAsync(_documento!.Id, dto, _session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo actualizar."; return; }
                SuccessMessage = "Cotización actualizada.";
                _documento = r.Value;
                IsDirty = false;

                DocumentSaved?.Invoke();
            }
            ReportarContexto();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task AgregarDetalleAsync(CancellationToken ct = default)
    {
        if (_session.Usuario is null) return;
        if (!IsNuevo && _documento is not null) return;
    }

    /// <summary>
    /// Punto de entrada institucional (ADR-040): agrega una nueva línea o incrementa
    /// la cantidad de la línea existente si el mismo producto ya está en la cotización.
    /// Recalcula totales automáticamente. Marca el documento como dirty.
    /// </summary>
    public async Task<bool> AgregarOIncrementarDetalleAsync(DetalleLineaEditable detalle)
    {
        var lineaExistente = ObtenerLineaExistente(detalle.ProductoId);
        if (lineaExistente is not null)
        {
            await IncrementarCantidadAsync(lineaExistente, detalle.Cantidad);
            return true;
        }
        return await AgregarDetalleLocalAsync(detalle);
    }

    /// <summary>
    /// Devuelve la línea existente con el mismo ProductoId, o <c>null</c> si no existe.
    /// </summary>
    public DetalleLineaEditable? ObtenerLineaExistente(int? productoId)
    {
        if (productoId is null) return null;
        return Detalles.FirstOrDefault(d => d.ProductoId == productoId);
    }

    /// <summary>
    /// Incrementa la cantidad de una línea existente y recalcula totales.
    /// Para documentos existentes persiste el cambio mediante eliminar + reagregar.
    /// </summary>
    public async Task IncrementarCantidadAsync(DetalleLineaEditable linea, decimal incremento)
    {
        var nuevaCantidad = linea.Cantidad + incremento;
        await ActualizarCantidadAsync(linea, nuevaCantidad);
    }

    /// <summary>
    /// Establece la cantidad de una línea (valor absoluto) con validación ERP (ADR-041).
    /// Reglas: cantidad debe ser &gt; 0; si es 0 elimina la línea; no se permiten negativos.
    /// Para documentos existentes persiste el cambio en BD inmediatamente.
    /// </summary>
    public async Task ActualizarCantidadAsync(DetalleLineaEditable linea, decimal nuevaCantidad)
    {
        if (nuevaCantidad < 0) return;    // Negativo: ignorar silenciosamente
        if (IsBusy)            return;    // Guard ADR-043: serializar; rechaza re-entrada concurrente

        if (nuevaCantidad == 0)
        {
            DetalleSeleccionado = linea;
            await EliminarDetalleCommand.ExecuteAsync(null);
            return;
        }

        if (IsNuevo)
        {
            linea.Cantidad = nuevaCantidad; // INPC notifica Importe automáticamente
        }
        else
        {
            if (_session.Usuario is null) return;
            IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
            try
            {
                var rEliminar = await _service.EliminarDetalleAsync(linea.Id, _session.Usuario.Id);
                if (!rEliminar.Success) { ErrorMessage = rEliminar.Error ?? "No se pudo actualizar la cantidad."; return; }

                var dto = new CrearDetalleLineaDto(linea.ProductoId, linea.Descripcion, nuevaCantidad, linea.PrecioUnitario, linea.DescuentoPct);
                var rAgregar = await _service.AgregarDetalleAsync(_documento!.Id, dto, _session.Usuario.Id);
                if (!rAgregar.Success) { ErrorMessage = rAgregar.Error ?? "No se pudo actualizar la cantidad."; return; }

                linea.Id       = rAgregar.Value!.Id;
                linea.Cantidad = nuevaCantidad;
                IsDirty = true;
            }
            finally { IsBusy = false; }
        }
    }

    /// <summary>
    /// Actualiza el descuento de una línea individual (ADR-042 / ADR-043).
    /// Para documentos NUEVOS: aplica en memoria (INPC + callback → RecalcularTotales).
    /// Para documentos EXISTENTES: persiste en BD mediante delete + readd.
    /// </summary>
    /// <remarks>
    /// Guard anti-reentrancy (ADR-043): si el valor ya está en el nivel deseado, retorna sin
    /// llamar al servicio. Esto evita la concurrencia DbContext cuando INPC de
    /// <see cref="DetalleLineaEditable.DescuentoPct"/> re-dispara
    /// <c>NumberBox_Descuento_ValueChanged</c>, que vuelve a llamar a este método con el
    /// mismo valor que ya fue aplicado por <see cref="AplicarDescuentoGlobalALineas"/>.
    /// </remarks>
    public async Task ActualizarDescuentoAsync(DetalleLineaEditable linea, decimal nuevoPct)
    {
        var pctClamped = Math.Clamp(nuevoPct, 0m, 100m);

        if (IsNuevo)
        {
            linea.DescuentoPct = pctClamped;
            return;
        }

        // Guard anti-reentrancy: el valor ya está aplicado en memoria — sin service call.
        // Ocurre cuando AplicarDescuentoGlobalALineas (Fase 1) ya pre-configuró el descuento
        // y el handler NumberBox_Descuento_ValueChanged se re-disparó por INPC.
        if (linea.DescuentoPct == pctClamped) return;

        if (_session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var rEliminar = await _service.EliminarDetalleAsync(linea.Id, _session.Usuario.Id);
            if (!rEliminar.Success) { ErrorMessage = rEliminar.Error ?? "No se pudo actualizar el descuento."; return; }

            var dto = new CrearDetalleLineaDto(linea.ProductoId, linea.Descripcion, linea.Cantidad, linea.PrecioUnitario, pctClamped);
            var rAgregar = await _service.AgregarDetalleAsync(_documento!.Id, dto, _session.Usuario.Id);
            if (!rAgregar.Success) { ErrorMessage = rAgregar.Error ?? "No se pudo actualizar el descuento."; return; }

            linea.Id           = rAgregar.Value!.Id;
            linea.DescuentoPct = pctClamped; // INPC notifica Importe + callback → RecalcularTotales
            IsDirty = true;
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Invalida silenciosamente el indicador de descuento global (ADR-042 / Global Discount Lifecycle).
    /// Se llama cuando el usuario modifica manualmente el descuento de UNA línea individual,
    /// lo que rompe la uniformidad del descuento global.
    ///
    /// <para>Regla de uniformidad: el descuento global solo es válido cuando TODAS las líneas
    /// tienen el mismo porcentaje. En cuanto una línea diverge, el concepto global es ambiguo.</para>
    ///
    /// <para>NO elimina los descuentos de línea existentes — solo borra el indicador global.
    /// NO muestra alertas — comportamiento silencioso y operacional.</para>
    /// </summary>
    internal void InvalidarDescuentoGlobal()
    {
        if (DescuentoGlobalPct != 0)
            DescuentoGlobalPct = 0;
    }

    /// <summary>
    /// Aplica el descuento global a todas las líneas, reemplazando descuentos individuales (ADR-042 / ADR-043).
    /// Debe llamarse solo después de confirmación del usuario cuando hay descuentos en líneas.
    /// </summary>
    /// <remarks>
    /// Patrón dos fases (ADR-043) para evitar concurrencia DbContext:
    ///
    /// FASE 1 — Memoria: establece <see cref="DetalleLineaEditable.DescuentoPct"/> en todas
    ///   las líneas ANTES de cualquier llamada al servicio. Los handlers ValueChanged del
    ///   NumberBox que se re-disparan por INPC encontrarán el guard en
    ///   <see cref="ActualizarDescuentoAsync"/> (valor ya igual) y retornan sin service call.
    ///
    /// FASE 2 — Persistencia (solo docs existentes): persiste cada línea con un único scope
    ///   IsBusy. NO vuelve a establecer DescuentoPct (ya hecho en Fase 1) para no re-disparar INPC.
    ///   Los handlers que se disparen durante los await de Fase 2 son rechazados por el guard IsBusy
    ///   en el code-behind.
    /// </remarks>
    public async Task AplicarDescuentoGlobalALineas(decimal pct)
    {
        var pctClamped = Math.Clamp(pct, 0m, 100m);
        DescuentoGlobalPct = pctClamped;

        // ── FASE 1: memoria (instantáneo, sin service calls) ───────────────────
        // INPC dispara NumberBox.ValueChanged por cada línea; el guard en ActualizarDescuentoAsync
        // detecta que el valor ya está aplicado y retorna sin llamar al servicio.
        foreach (var linea in Detalles.ToList())
            linea.DescuentoPct = pctClamped;

        RecalcularTotales();
        IsDirty = true;

        if (IsNuevo) return;

        // ── FASE 2: persistencia en BD (docs existentes, scope único IsBusy) ──
        if (_session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            foreach (var linea in Detalles.ToList())
            {
                var rEliminar = await _service.EliminarDetalleAsync(linea.Id, _session.Usuario.Id);
                if (!rEliminar.Success) { ErrorMessage = rEliminar.Error ?? "Error al actualizar descuento."; return; }

                var dto = new CrearDetalleLineaDto(linea.ProductoId, linea.Descripcion, linea.Cantidad, linea.PrecioUnitario, pctClamped);
                var rAgregar = await _service.AgregarDetalleAsync(_documento!.Id, dto, _session.Usuario.Id);
                if (!rAgregar.Success) { ErrorMessage = rAgregar.Error ?? "Error al actualizar línea."; return; }

                // Solo actualizar el ID — DescuentoPct ya fue establecido en Fase 1
                linea.Id = rAgregar.Value!.Id;
            }
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Limpia el descuento global y todos los descuentos de línea (ADR-042 / ADR-043).
    /// Mismo patrón dos fases que <see cref="AplicarDescuentoGlobalALineas"/>.
    /// </summary>
    public async Task LimpiarDescuentoGlobal()
    {
        DescuentoGlobalPct = 0;

        // FASE 1: memoria
        foreach (var linea in Detalles.ToList())
            linea.DescuentoPct = 0;

        RecalcularTotales();
        IsDirty = true;

        if (IsNuevo) return;

        // FASE 2: persistencia BD
        if (_session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            foreach (var linea in Detalles.ToList())
            {
                var rEliminar = await _service.EliminarDetalleAsync(linea.Id, _session.Usuario.Id);
                if (!rEliminar.Success) { ErrorMessage = rEliminar.Error ?? "Error al limpiar descuento."; return; }

                var dto = new CrearDetalleLineaDto(linea.ProductoId, linea.Descripcion, linea.Cantidad, linea.PrecioUnitario, 0m);
                var rAgregar = await _service.AgregarDetalleAsync(_documento!.Id, dto, _session.Usuario.Id);
                if (!rAgregar.Success) { ErrorMessage = rAgregar.Error ?? "Error al actualizar línea."; return; }

                linea.Id = rAgregar.Value!.Id;
            }
        }
        finally { IsBusy = false; }
    }

    public async Task<bool> AgregarDetalleLocalAsync(DetalleLineaEditable detalle)
    {
        if (IsNuevo)
        {
            Detalles.Add(WirarLinea(detalle));
            IsDirty = true;
            return true;
        }
        else
        {
            if (_session.Usuario is null) return false;
            IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
            try
            {
                var dto = new CrearDetalleLineaDto(detalle.ProductoId, detalle.Descripcion, detalle.Cantidad, detalle.PrecioUnitario, detalle.DescuentoPct);
                var r   = await _service.AgregarDetalleAsync(_documento!.Id, dto, _session.Usuario.Id);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo agregar."; return false; }
                Detalles.Add(WirarLinea(new DetalleLineaEditable(
                    r.Value!.Id, r.Value.ProductoId, r.Value.Descripcion,
                    r.Value.Cantidad, r.Value.PrecioUnitario,
                    sku: detalle.Sku,               // Preservar SKU del detalle original
                    ivaAplicable: detalle.IvaAplicable,
                    descuentoPct: r.Value.DescuentoPct)));
                IsDirty = true;
                RecalcularTotales();
                return true;
            }
            finally { IsBusy = false; }
        }
    }

    private bool HayDetalleSeleccionado => DetalleSeleccionado is not null;

    [RelayCommand(CanExecute = nameof(HayDetalleSeleccionado))]
    public async Task EliminarDetalleAsync(CancellationToken ct = default)
    {
        if (DetalleSeleccionado is null) return;
        if (IsNuevo)
        {
            Detalles.Remove(DetalleSeleccionado);
            DetalleSeleccionado = null;
            IsDirty = true;
            return;
        }
        if (_session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.EliminarDetalleAsync(DetalleSeleccionado.Id, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo eliminar."; return; }
            Detalles.Remove(DetalleSeleccionado);
            DetalleSeleccionado = null;
            IsDirty = true;
            RecalcularTotales();
        }
        finally { IsBusy = false; }
    }

    // ── Cargos — Commercial Charges Pattern ──────────────────────────────────

    [RelayCommand]
    public async Task AgregarCargoAsync(CancellationToken ct = default)
    {
        if (SolicitarAgregarCargo is null) return;
        var cargo = await SolicitarAgregarCargo();
        if (cargo is null) return;

        if (IsNuevo)
        {
            // Nuevo documento: cargo en memoria, se persistirá en GuardarAsync
            cargo.Id = 0;
            Cargos.Add(cargo);
            IsDirty = true;
            RecalcularTotales();
            OnPropertyChanged(nameof(HayCargos));
            return;
        }

        // Documento existente: persistir inmediatamente
        if (_session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var dto = new Application.DTOs.Ventas.CrearCotizacionCargoDto(
                cargo.OtroCargoId, cargo.Descripcion, cargo.Importe, cargo.AplicaIva, Cargos.Count);
            var r = await _service.AgregarCargoAsync(_documento!.Id, dto, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo agregar el cargo."; return; }
            cargo.Id = r.Value!.Id;
            Cargos.Add(cargo);
            IsDirty = true;
            RecalcularTotales();
            OnPropertyChanged(nameof(HayCargos));
        }
        finally { IsBusy = false; }
    }

    private bool HayCargoSeleccionado => CargoSeleccionado is not null;

    [RelayCommand(CanExecute = nameof(HayCargoSeleccionado))]
    public async Task EliminarCargoAsync(CancellationToken ct = default)
    {
        if (CargoSeleccionado is null) return;

        if (IsNuevo || CargoSeleccionado.Id == 0)
        {
            Cargos.Remove(CargoSeleccionado);
            CargoSeleccionado = null;
            IsDirty = true;
            RecalcularTotales();
            OnPropertyChanged(nameof(HayCargos));
            return;
        }

        if (_session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.EliminarCargoAsync(CargoSeleccionado.Id, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo eliminar el cargo."; return; }
            Cargos.Remove(CargoSeleccionado);
            CargoSeleccionado = null;
            IsDirty = true;
            RecalcularTotales();
            OnPropertyChanged(nameof(HayCargos));
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task CambiarEstatusAsync(EstatusCotizacion nuevoEstatus, CancellationToken ct = default)
    {
        if (_documento is null || _session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.CambiarEstatusAsync(_documento.Id, nuevoEstatus, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo cambiar el estado."; return; }
            Estatus = nuevoEstatus;
            _documento = _documento with { Estatus = nuevoEstatus };
            SuccessMessage = $"Estado actualizado → {EstatusTextoDisplay}";
            RefrescarPermisosUI();
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Flujo completo de aprobación (PARTE 2): Validar → Guardar → Aprobar.
    /// Garantiza que el snapshot comercial sea la versión final antes de congelar el documento.
    /// Si el guardado falla, la aprobación se cancela.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeAprobar))]
    public async Task AprobarConGuardadoAsync(CancellationToken ct = default)
    {
        if (_session.Usuario is null || !PuedeAprobar) return;

        // Validaciones previas a la aprobación
        if (_entidadDirectorioSeleccionada is null && !RelacionComercialId.HasValue)
        {
            ErrorMessage = "Debe seleccionar un cliente antes de aprobar.";
            return;
        }
        if (Detalles.Count == 0)
        {
            ErrorMessage = "Debe agregar al menos una línea de detalle antes de aprobar.";
            return;
        }

        // Guardar primero si hay cambios pendientes — garantiza snapshot correcto
        if (IsDirty || IsNuevo)
        {
            await GuardarAsync(ct);
            if (!string.IsNullOrEmpty(ErrorMessage)) return; // Guardado falló — no aprobar
            if (!PuedeAprobar) return;                       // Estado inválido post-guardado
        }

        await CambiarEstatusAsync(EstatusCotizacion.Aprobada, ct);
    }

    [RelayCommand]
    public async Task ConvertirAPedidoAsync(CancellationToken ct = default)
    {
        if (_documento is null || _session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.ConvertirAPedidoAsync(_documento.Id, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo convertir."; return; }
            SuccessMessage = $"Pedido #{r.Value!.Id} generado.";
            NotificarPedidoGenerado?.Invoke(r.Value);
        }
        finally { IsBusy = false; }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Conecta el callback de recálculo a la línea (ADR-041 / ADR-042).
    /// Debe llamarse en TODA ruta que crea una <see cref="DetalleLineaEditable"/>.
    /// El mismo callback maneja cambios de Cantidad y de DescuentoPct.
    /// </summary>
    private DetalleLineaEditable WirarLinea(DetalleLineaEditable linea)
    {
        linea.ValorCambiadoCallback = () =>
        {
            IsDirty = true;
            RecalcularTotales();
        };
        return linea;
    }

    /// <summary>
    /// Recalcula todos los totales del documento a partir de las líneas actuales (ADR-040 / ADR-042).
    /// Single Source of Truth para aritmética — NO duplicar en otros métodos.
    /// </summary>
    private void RecalcularTotales()
    {
        SubtotalBruto  = Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);
        Subtotal       = Detalles.Sum(d => d.Importe);  // neto con descuento
        DescuentoTotal = SubtotalBruto - Subtotal;

        // Commercial Charges Pattern: cargos en sección separada
        TotalOtrosCargos = Cargos.Sum(c => c.Importe);

        // IVA sobre productos + IVA sobre cargos que aplican (Commercial Tax Pattern)
        var ivaProductos = CommercialDocumentCalculator.CalcularImpuestos(
            Detalles.Select(d => (d.Importe, d.IvaAplicable)), _tasaIva);
        var ivaCargos    = CommercialDocumentCalculator.CalcularImpuestos(
            Cargos.Select(c => (c.Importe, c.AplicaIva)), _tasaIva);
        Impuestos = ivaProductos + ivaCargos;

        // Total = Subtotal (productos) + OtrosCargos + Impuestos
        Total          = CommercialDocumentCalculator.CalcularTotal(Subtotal + TotalOtrosCargos, Impuestos);
        TotalArticulos = Detalles.Sum(d => d.Cantidad);
        OnPropertyChanged(nameof(HayDescuento));
        OnPropertyChanged(nameof(HayDescuentosEnLineas));
    }

    /// <summary>
    /// Detecta si todas las líneas cargadas tienen el mismo descuento > 0 (indica descuento global anterior).
    /// Restaura <see cref="DescuentoGlobalPct"/> si es el caso.
    /// </summary>
    private void DetectarDescuentoGlobal(IReadOnlyList<DetalleLineaDto> detalles)
    {
        if (detalles.Count == 0) return;
        var primerPct = detalles[0].DescuentoPct;
        if (primerPct > 0 && detalles.All(d => d.DescuentoPct == primerPct))
            DescuentoGlobalPct = primerPct;
        else
            DescuentoGlobalPct = 0;
    }

    private void RefrescarPermisosUI()
    {
        OnPropertyChanged(nameof(PuedeEditar));
        OnPropertyChanged(nameof(PuedeEnviar));
        OnPropertyChanged(nameof(PuedeAprobar));
        OnPropertyChanged(nameof(PuedeConvertir));
        OnPropertyChanged(nameof(PuedeCancelar));
        OnPropertyChanged(nameof(EstatusTextoDisplay));
        OnPropertyChanged(nameof(MostrarOperacionesRealizadas));
        OnPropertyChanged(nameof(ResumenOperacion));

        // Notificar CanExecute del command de aprobación con guardado.
        AprobarConGuardadoCommand.NotifyCanExecuteChanged();

        // Propagar estado de edición a ítems de líneas y cargos (DataTemplate bind a item.PuedeEditar).
        var puedeEditar = PuedeEditarLineas;   // true solo Borrador
        foreach (var d in Detalles)
            d.PuedeEditar = puedeEditar;
        foreach (var c in Cargos)
            c.PuedeEditar = puedeEditar;
    }

    private void ReportarContexto()
    {
        var clienteInfo = RelacionComercialId.HasValue ? $"Cliente: {NombreCliente} (ID={RelacionComercialId})" : "Sin cliente";
        _contextTracker.SetViewModelContext(new CurrentOperationalContext(
            Module: "Ventas", SubModule: "Cotizaciones",
            ViewModel: nameof(CotizacionDocumentoViewModel),
            Entity: "ventas.Cotizacion",
            RecordCount: Detalles.Count,
            SearchTerm: null, SoloActivos: false,
            CategoriaFiltro: IsNuevo ? $"NUEVA — {clienteInfo}" : $"#{_documento?.Id} — {clienteInfo}",
            FiltroTemporal: null,
            EmpresaFilter:    new(FilterState.Applied, _session.EmpresaId.ToString()),
            SucursalFilter:   new(FilterState.OmittedExpected, Note: "Cotizaciones por empresa"),
            AlmacenFilter:    new(FilterState.OmittedExpected, Note: "No aplica"),
            SoftDeleteFilter: new(FilterState.Applied, "Borrado = false"),
            Source: "WorkspaceTab", UpdatedAt: DateTime.Now));
    }
}

/// <summary>
/// Línea de detalle editable en memoria para el documento.
/// Id = 0 para líneas nuevas no guardadas; > 0 para líneas ya persistidas.
/// Implementa <see cref="INotifyPropertyChanged"/> para que el ListView refleje cambios de cantidad,
/// descuento e importe sin necesidad de guardar (ADR-041 + ADR-042).
/// </summary>
/// <remarks>
/// Importe = Cantidad × PrecioUnitario × (1 − DescuentoPct/100) — calculado via
/// <see cref="CommercialDocumentCalculator.CalcularImporteLinea"/>, Single Source of Truth.
/// Se persiste en BD cuando el servicio llama a SaveChangesAsync.
/// </remarks>
public sealed class DetalleLineaEditable : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    // ── Callback al ViewModel para recalcular totales ─────────────────────────
    /// <summary>
    /// Invocado cuando cambia la cantidad o el descuento. El ViewModel lo conecta a RecalcularTotales().
    /// </summary>
    internal Action? ValorCambiadoCallback { get; set; }

    // ── Control de edición (propagado desde ViewModel según estado de cotización) ─

    private bool _puedeEditar = true;

    /// <summary>
    /// Controla si los controles de edición de línea (Cantidad, Desc. %) están habilitados.
    /// El ViewModel lo actualiza en <c>RefrescarPermisosUI</c> según <c>EstatusCotizacion</c>.
    /// </summary>
    public bool PuedeEditar
    {
        get => _puedeEditar;
        set
        {
            if (_puedeEditar == value) return;
            _puedeEditar = value;
            OnPropertyChanged(nameof(PuedeEditar));
        }
    }

    public DetalleLineaEditable() { }

    /// <param name="id">0 para líneas nuevas no guardadas; &gt;0 para líneas ya persistidas.</param>
    /// <param name="sku">Código/SKU del producto. Solo informativo.</param>
    /// <param name="ivaAplicable">Si <c>true</c>, el producto tiene IVA estándar (ADR-040).</param>
    /// <param name="descuentoPct">Porcentaje de descuento de la línea (0–100, ADR-042).</param>
    public DetalleLineaEditable(long id, int? productoId, string descripcion, decimal cantidad, decimal precioUnitario,
        string? sku = null, bool ivaAplicable = false, decimal descuentoPct = 0m)
    {
        Id             = id;
        ProductoId     = productoId;
        Descripcion    = descripcion;
        _cantidad      = cantidad;
        _precioUnitario = precioUnitario;
        Sku            = sku;
        IvaAplicable   = ivaAplicable;
        _descuentoPct  = Math.Clamp(descuentoPct, 0m, 100m);
    }

    public long    Id         { get; set; }
    public int?    ProductoId { get; set; }
    public string  Descripcion { get; set; } = string.Empty;

    // ── Cantidad con notificación INPC ────────────────────────────────────────

    private decimal _cantidad;

    /// <summary>
    /// Cantidad de la línea.
    /// Al cambiar notifica Cantidad, CantidadDouble e Importe, y dispara ValorCambiadoCallback.
    /// </summary>
    public decimal Cantidad
    {
        get => _cantidad;
        set => SetCantidad(value);
    }

    /// <summary>Wrapper double de Cantidad para el NumberBox de WinUI (ADR-041).</summary>
    public double CantidadDouble
    {
        get => (double)_cantidad;
        set => SetCantidad((decimal)value);
    }

    private void SetCantidad(decimal value)
    {
        if (_cantidad == value) return;
        _cantidad = value;
        OnPropertyChanged(nameof(Cantidad));
        OnPropertyChanged(nameof(CantidadDouble));
        OnPropertyChanged(nameof(Importe));
        OnPropertyChanged(nameof(DescuentoImporte));
        ValorCambiadoCallback?.Invoke();
    }

    // ── PrecioUnitario con notificación INPC ──────────────────────────────────

    private decimal _precioUnitario;

    /// <summary>Precio unitario. Al cambiar notifica PrecioUnitario, Importe y DescuentoImporte.</summary>
    public decimal PrecioUnitario
    {
        get => _precioUnitario;
        set
        {
            if (_precioUnitario == value) return;
            _precioUnitario = value;
            OnPropertyChanged(nameof(PrecioUnitario));
            OnPropertyChanged(nameof(Importe));
            OnPropertyChanged(nameof(DescuentoImporte));
        }
    }

    // ── DescuentoPct con notificación INPC ────────────────────────────────────

    private decimal _descuentoPct;

    /// <summary>
    /// Porcentaje de descuento de la línea (0–100, ADR-042).
    /// Al cambiar notifica DescuentoPct, DescuentoPctDouble, DescuentoImporte e Importe,
    /// y dispara ValorCambiadoCallback para recalcular totales del documento.
    /// </summary>
    public decimal DescuentoPct
    {
        get => _descuentoPct;
        set => SetDescuentoPct(value);
    }

    /// <summary>Wrapper double de DescuentoPct para el NumberBox en el grid.</summary>
    public double DescuentoPctDouble
    {
        get => (double)_descuentoPct;
        set => SetDescuentoPct((decimal)value);
    }

    private void SetDescuentoPct(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 100m);
        if (_descuentoPct == clamped) return;
        _descuentoPct = clamped;
        OnPropertyChanged(nameof(DescuentoPct));
        OnPropertyChanged(nameof(DescuentoPctDouble));
        OnPropertyChanged(nameof(DescuentoImporte));
        OnPropertyChanged(nameof(Importe));
        ValorCambiadoCallback?.Invoke();
    }

    /// <summary>SKU/código del producto. Solo informativo en el grid.</summary>
    public string? Sku { get; set; }

    /// <summary>
    /// Indica si el producto tiene IVA estándar aplicable (ADR-040).
    /// Basado en <c>Producto.IvaAplicable</c> al momento de agregar la línea.
    /// </summary>
    public bool IvaAplicable { get; set; }

    /// <summary>Texto para columna IVA del grid ("Sí" / "No").</summary>
    public string IvaTexto => IvaAplicable ? "Sí" : "No";

    /// <summary>
    /// Importe neto = Cantidad × PrecioUnitario × (1 − DescuentoPct/100).
    /// Calculado via <see cref="CommercialDocumentCalculator"/> — Single Source of Truth.
    /// Se notifica automáticamente cuando cambian Cantidad, PrecioUnitario o DescuentoPct.
    /// </summary>
    public decimal Importe
        => CommercialDocumentCalculator.CalcularImporteLinea(_cantidad, _precioUnitario, _descuentoPct);

    /// <summary>
    /// Monto monetario del descuento de esta línea (ADR-042).
    /// Se notifica automáticamente cuando cambian Cantidad, PrecioUnitario o DescuentoPct.
    /// </summary>
    public decimal DescuentoImporte
        => CommercialDocumentCalculator.CalcularDescuentoLinea(_cantidad, _precioUnitario, _descuentoPct);
}

/// <summary>
/// Cargo accesorio editable en memoria para el documento de cotización.
/// Commercial Charges Pattern: sección separada de las líneas de producto.
/// Id = 0 para cargos nuevos no persistidos; > 0 para cargos ya guardados en BD.
/// </summary>
public sealed class CargoLineaEditable : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    public long    Id          { get; set; }
    public int?    OtroCargoId { get; set; }
    public string  Descripcion { get; set; }
    public decimal Importe     { get; set; }
    public bool    AplicaIva   { get; set; }

    /// <summary>Texto para columna IVA del grid.</summary>
    public string IvaTexto => AplicaIva ? "Sí" : "No";

    private bool _puedeEditar = true;

    /// <summary>
    /// Controla si el botón de eliminar cargo está habilitado.
    /// El ViewModel lo actualiza en <c>RefrescarPermisosUI</c> según <c>EstatusCotizacion</c>.
    /// </summary>
    public bool PuedeEditar
    {
        get => _puedeEditar;
        set
        {
            if (_puedeEditar == value) return;
            _puedeEditar = value;
            OnPropertyChanged(nameof(PuedeEditar));
        }
    }

    public CargoLineaEditable(long id, int? otroCargoId, string descripcion, decimal importe, bool aplicaIva)
    {
        Id          = id;
        OtroCargoId = otroCargoId;
        Descripcion = descripcion;
        Importe     = importe;
        AplicaIva   = aplicaIva;
    }
}

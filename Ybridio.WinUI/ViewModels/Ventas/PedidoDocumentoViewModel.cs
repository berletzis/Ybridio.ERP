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
/// ViewModel de documento para el formulario de pedido (nuevo o existente).
/// Commercial Document Surface institucional — equivalente a CotizacionDocumentoViewModel.
/// Gestiona encabezado, detalles, cargos, workflow y cálculos via CommercialDocumentCalculator.
/// </summary>
/// <remarks>
/// Fórmulas (ADR-040 / ADR-042 — CommercialDocumentCalculator):
/// - SubtotalBruto = SUM(Cantidad × PrecioUnitario)
/// - Subtotal      = SUM(Detalles.Importe)  [neto con descuento]
/// - Impuestos     = SUM(líneas IVA) × TasaIva + SUM(cargos IVA) × TasaIva
/// - Total         = Subtotal + TotalOtrosCargos + Impuestos
/// </remarks>
public sealed partial class PedidoDocumentoViewModel : ObservableObject
{
    private readonly IPedidoService                   _service;
    private readonly IVentaDocumentalService          _ventaService;
    private readonly IRelacionComercialService         _relacionComercialService;
    private readonly IErpAuthorizationService         _auth;
    private readonly SessionService                   _session;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;
    private readonly IConfiguracionFiscalService      _configuracionFiscal;

    /// <summary>Tasa IVA activa. Fallback a estándar hasta que CargarConfiguracionFiscalAsync complete.</summary>
    private decimal _tasaIva = FiscalConstants.TasaIvaEstandar;

    private PedidoDto? _documento;
    // Entidad de Directorio seleccionada (ADR-038). RelacionComercialId se resuelve en GuardarAsync.
    private DirectorioSelectorDto? _entidadDirectorioSeleccionada;
    private bool _clienteModificadoPorUsuario;

    [ObservableProperty] private bool          isNuevo = true;
    [ObservableProperty] private bool          isBusy;
    [ObservableProperty] private string        errorMessage   = string.Empty;
    [ObservableProperty] private string        successMessage = string.Empty;

    [ObservableProperty] private string        nombreCliente  = string.Empty;
    [ObservableProperty] private int?          _relacionComercialId;
    [ObservableProperty] private DateTime      fecha          = DateTime.Today;
    [ObservableProperty] private DateTime?     fechaEntregaCompromiso;

    /// <summary>Wrapper DateTimeOffset para el DatePicker de Fecha.</summary>
    public DateTimeOffset FechaOffset
    {
        get => new DateTimeOffset(Fecha);
        set => Fecha = value.DateTime;
    }

    /// <summary>Wrapper DateTimeOffset para el DatePicker de FechaEntregaCompromiso.</summary>
    public DateTimeOffset FechaEntregaCompromisoOffset
    {
        get => FechaEntregaCompromiso.HasValue ? new DateTimeOffset(FechaEntregaCompromiso.Value) : DateTimeOffset.Now.AddDays(7);
        set => FechaEntregaCompromiso = value.DateTime;
    }

    partial void OnFechaChanged(DateTime value)                    => OnPropertyChanged(nameof(FechaOffset));
    partial void OnFechaEntregaCompromisoChanged(DateTime? value)  => OnPropertyChanged(nameof(FechaEntregaCompromisoOffset));
    [ObservableProperty] private string?       observaciones;
    [ObservableProperty] private EstatusPedido estatus        = EstatusPedido.Borrador;
    [ObservableProperty] private string?       folio;

    public ObservableCollection<DetalleLineaEditable> Detalles { get; } = [];
    public ObservableCollection<CargoLineaEditable>   Cargos   { get; } = [];

    // ── Totales (CommercialDocumentCalculator — ADR-042) ──────────────────────
    [ObservableProperty] private decimal subtotalBruto;
    [ObservableProperty] private decimal subtotal;
    [ObservableProperty] private decimal descuentoTotal;
    [ObservableProperty] private decimal totalOtrosCargos;
    [ObservableProperty] private decimal impuestos;
    [ObservableProperty] private decimal total;
    [ObservableProperty] private decimal totalArticulos;

    public bool HayDescuento          => DescuentoTotal > 0;
    public bool HayDescuentosEnLineas => Detalles.Any(d => d.DescuentoPct > 0);
    public bool HayCargos             => Cargos.Count > 0;

    // ── Descuento global (ADR-042) ────────────────────────────────────────────
    [ObservableProperty] private decimal descuentoGlobalPct;
    public double DescuentoGlobalPctDouble => (double)DescuentoGlobalPct;
    partial void OnDescuentoGlobalPctChanged(decimal value) => OnPropertyChanged(nameof(DescuentoGlobalPctDouble));

    // ── Cargo seleccionado ────────────────────────────────────────────────────
    [ObservableProperty] private CargoLineaEditable? cargoSeleccionado;

    // ── Info de cliente seleccionado ──────────────────────────────────────────
    [ObservableProperty] private string? clienteEmail;
    [ObservableProperty] private string? clienteTelefono;
    public bool TieneClienteSeleccionado => _entidadDirectorioSeleccionada is not null || RelacionComercialId.HasValue;

    /// <summary>Callback para abrir el diálogo de agregar cargo — la Page lo asigna.</summary>
    public Func<Task<CargoLineaEditable?>>? SolicitarAgregarCargo { get; set; }

    /// <summary>Título del documento: usa el folio cuando existe, o el ID como fallback.</summary>
    public string TituloDocumento => IsNuevo
        ? "Nuevo Pedido"
        : !string.IsNullOrEmpty(Folio) ? $"Pedido {Folio}" : $"Pedido #{_documento?.Id}";

    public string EstatusTextoDisplay => Estatus switch
    {
        EstatusPedido.Borrador   => "Borrador",
        EstatusPedido.Autorizado => "Autorizado",
        EstatusPedido.EnProceso  => "En Proceso",
        EstatusPedido.Parcial    => "Parcial",
        EstatusPedido.Finalizado => "Finalizado",
        EstatusPedido.Cancelado  => "Cancelado",
        _                        => Estatus.ToString()
    };

    // ── Workflow guards ────────────────────────────────────────────────────────
    /// <summary>Se puede editar encabezado y líneas mientras no esté Finalizado ni Cancelado.</summary>
    public bool PuedeEditar      => Estatus is not (EstatusPedido.Finalizado or EstatusPedido.Cancelado);
    /// <summary>Se puede avanzar estado linealmente hasta Finalizado.</summary>
    public bool PuedeAvanzar     => Estatus is EstatusPedido.Borrador or EstatusPedido.Autorizado or EstatusPedido.EnProceso or EstatusPedido.Parcial;
    /// <summary>Se puede generar OT desde Autorizado en adelante (documento guardado).</summary>
    public bool PuedeGenerarOT   => !IsNuevo && Estatus is EstatusPedido.Autorizado or EstatusPedido.EnProceso or EstatusPedido.Parcial;
    /// <summary>Se puede cancelar siempre que no esté Cancelado ni Finalizado.</summary>
    public bool PuedeCancelar    => !IsNuevo && Estatus is not (EstatusPedido.Finalizado or EstatusPedido.Cancelado);
    /// <summary>Se puede generar Venta en cualquier estado operacional (no Cancelado).</summary>
    public bool PuedeGenerarVenta => !IsNuevo && Estatus != EstatusPedido.Cancelado;

    /// <summary>ID de empresa del contexto de sesión, expuesto para binding en el selector control.</summary>
    public int EmpresaId => _session.EmpresaId;

    public EstatusPedido SiguienteEstatus => Estatus switch
    {
        EstatusPedido.Borrador   => EstatusPedido.Autorizado,
        EstatusPedido.Autorizado => EstatusPedido.EnProceso,
        EstatusPedido.EnProceso  => EstatusPedido.Finalizado,
        EstatusPedido.Parcial    => EstatusPedido.Finalizado,
        _                        => Estatus
    };

    /// <summary>
    /// Retorna las transiciones válidas desde el estado actual del pedido.
    /// Centraliza la lógica de workflow — NO duplicar inline en XAML ni code-behind.
    /// </summary>
    /// <returns>Lista de (EstatusPedido destino, etiqueta para UI).</returns>
    public IReadOnlyList<(EstatusPedido Estado, string Etiqueta)> GetAvailableTransitions()
    {
        if (IsNuevo) return [];
        return Estatus switch
        {
            EstatusPedido.Borrador   => [(EstatusPedido.Autorizado, "Autorizar")],
            EstatusPedido.Autorizado => [(EstatusPedido.EnProceso,  "Poner en Proceso")],
            EstatusPedido.EnProceso  =>
            [
                (EstatusPedido.Parcial,    "Marcar Parcial"),
                (EstatusPedido.Finalizado, "Finalizar")
            ],
            EstatusPedido.Parcial    => [(EstatusPedido.Finalizado, "Finalizar")],
            _                        => []
        };
    }

    /// <summary>Avanza el pedido a un estado específico (destino directo del menú contextual).</summary>
    public async Task AvanzarAEstatusAsync(EstatusPedido destino, CancellationToken ct = default)
    {
        if (_documento is null || _session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.CambiarEstatusAsync(_documento.Id, destino, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo cambiar el estado."; return; }
            Estatus = destino; _documento = _documento with { Estatus = Estatus };
            SuccessMessage = $"Estado → {EstatusTextoDisplay}"; RefrescarPermisosUI();
        }
        finally { IsBusy = false; }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EliminarDetalleCommand))]
    private DetalleLineaEditable? detalleSeleccionado;

    public Action<OrdenTrabajoDto>?    NotificarOTGenerada;
    public Action<VentaDocumentalDto>? NotificarVentaGenerada;

    /// <summary>
    /// Selecciona una entidad del Directorio desde el selector institucional (ADR-038).
    /// RelacionComercialId se resolverá mediante GetOrCreate al guardar.
    /// </summary>
    public void SeleccionarCliente(DirectorioSelectorDto? entidad)
    {
        _entidadDirectorioSeleccionada = entidad;
        _clienteModificadoPorUsuario   = true;
        NombreCliente    = entidad?.DisplayName ?? string.Empty;
        ClienteEmail     = entidad?.Email;
        ClienteTelefono  = entidad?.Telefono;
        OnPropertyChanged(nameof(TieneClienteSeleccionado));
    }

    /// <summary>Limpia la selección de cliente.</summary>
    public void LimpiarCliente()
    {
        _entidadDirectorioSeleccionada = null;
        _clienteModificadoPorUsuario   = true;
        RelacionComercialId = null;
        NombreCliente       = string.Empty;
        ClienteEmail        = null;
        ClienteTelefono     = null;
        OnPropertyChanged(nameof(TieneClienteSeleccionado));
    }

    public PedidoDocumentoViewModel(
        IPedidoService                   service,
        IVentaDocumentalService          ventaService,
        IRelacionComercialService         relacionComercialService,
        IErpAuthorizationService         auth,
        SessionService                   session,
        IOperationalObservabilityService observability,
        ICurrentContextTracker           contextTracker,
        IConfiguracionFiscalService      configuracionFiscal)
    {
        _service                   = service;
        _ventaService              = ventaService;
        _relacionComercialService  = relacionComercialService;
        _auth                = auth;
        _session             = session;
        _observability       = observability;
        _contextTracker      = contextTracker;
        _configuracionFiscal = configuracionFiscal;
        Detalles.CollectionChanged += (_, _) => RecalcularTotales();
        Cargos.CollectionChanged   += (_, _) => { RecalcularTotales(); OnPropertyChanged(nameof(HayCargos)); };
    }

    /// <summary>Carga la tasa IVA desde IConfiguracionFiscalService. Fire-and-forget.</summary>
    public async Task CargarConfiguracionFiscalAsync(CancellationToken ct = default)
    {
        try
        {
            _tasaIva = await _configuracionFiscal.ObtenerTasaIvaProductoAsync(ct);
            RecalcularTotales();
        }
        catch { /* Fallback FiscalConstants.TasaIvaEstandar ya activo */ }
    }

    public void Initialize(PedidoDto? pedido)
    {
        _documento = pedido;
        IsNuevo    = pedido is null;

        if (pedido is not null)
        {
            NombreCliente          = pedido.NombreCliente;
            RelacionComercialId    = pedido.RelacionComercialId;
            Fecha                  = pedido.Fecha;
            FechaEntregaCompromiso = pedido.FechaEntregaCompromiso;
            Observaciones          = pedido.Observaciones;
            Estatus                = pedido.Estatus;
            Folio                  = pedido.Folio;

            // Sintetizar entidad directorio para chip del selector
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
            foreach (var d in pedido.Detalles)
                Detalles.Add(WirarLinea(new DetalleLineaEditable(
                    d.Id, d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario,
                    sku: d.Sku, ivaAplicable: d.IvaAplicable, descuentoPct: d.DescuentoPct)));

            Cargos.Clear();
            if (pedido.Cargos is not null)
                foreach (var c in pedido.Cargos.OrderBy(c => c.Orden))
                    Cargos.Add(new CargoLineaEditable(c.Id, null, c.Descripcion, c.Importe, c.AplicaIva));

            DetectarDescuentoGlobal(pedido.Detalles);
            OnPropertyChanged(nameof(TieneClienteSeleccionado));
        }

        RecalcularTotales();
        OnPropertyChanged(nameof(TituloDocumento));
        RefrescarPermisosUI();
    }

    private void DetectarDescuentoGlobal(IReadOnlyList<DetalleLineaDto> detalles)
    {
        if (detalles.Count == 0) return;
        var primerPct = detalles[0].DescuentoPct;
        DescuentoGlobalPct = (primerPct > 0 && detalles.All(d => d.DescuentoPct == primerPct))
            ? primerPct : 0;
    }

    [RelayCommand]
    public async Task GuardarAsync(CancellationToken ct = default)
    {
        if (_session.Usuario is null) return;
        if (string.IsNullOrWhiteSpace(NombreCliente)) { ErrorMessage = "El nombre del cliente es obligatorio."; return; }

        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            // ADR-038: GetOrCreate RelacionComercial — solo si el usuario cambió el cliente
            if (_entidadDirectorioSeleccionada is not null && _clienteModificadoPorUsuario)
            {
                var rc = await _relacionComercialService.GetOrCreateAsync(
                    _session.EmpresaId, _entidadDirectorioSeleccionada, _session.Usuario.Id, ct);
                if (!rc.Success) { ErrorMessage = rc.Error ?? "No se pudo vincular el cliente."; return; }
                RelacionComercialId = rc.Value;
            }
            if (IsNuevo)
            {
                var dto = new CrearPedidoDto(
                    _session.EmpresaId, _session.SucursalId != 0 ? _session.SucursalId : null,
                    RelacionComercialId, NombreCliente, null, Fecha, FechaEntregaCompromiso, Observaciones,
                    Detalles.Select(d => new CrearDetalleLineaDto(
                        d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario,
                        d.DescuentoPct, d.IvaAplicable)).ToList());

                var r = await _service.CrearAsync(dto, _session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo crear."; return; }

                // Persistir cargos acumulados en memoria (mismo patrón que Cotizacion)
                var pedidoId = r.Value!.Id;
                var userId   = _session.Usuario.Id;
                var cargosMem = Cargos.ToList();
                for (int i = 0; i < cargosMem.Count; i++)
                {
                    var cc = cargosMem[i];
                    var rc2 = await _service.AgregarCargoAsync(pedidoId,
                        new CrearPedidoCargoDto(cc.Descripcion, cc.Importe, cc.AplicaIva, i), userId, ct);
                    if (rc2.Success) cc.Id = rc2.Value!.Id;
                }

                // Recargar desde BD para tener snapshot correcto (Sku, Cargos, etc.)
                var reloaded = await _service.ObtenerConDetallesAsync(pedidoId, ct);
                var dtoFinal = reloaded.Success ? reloaded.Value! : r.Value;
                var idDisplay = !string.IsNullOrEmpty(dtoFinal.Folio) ? dtoFinal.Folio : $"#{dtoFinal.Id}";
                SuccessMessage = $"Pedido {idDisplay} creado.";
                Initialize(dtoFinal);
            }
            else
            {
                var dto = new ActualizarPedidoDto(RelacionComercialId, NombreCliente, Fecha, FechaEntregaCompromiso, Observaciones);
                var r   = await _service.ActualizarAsync(_documento!.Id, dto, _session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo actualizar."; return; }
                SuccessMessage = "Pedido actualizado.";
                _documento = r.Value;
            }
        }
        finally { IsBusy = false; }
    }

    public async Task<bool> AgregarDetalleLocalAsync(DetalleLineaEditable detalle)
    {
        if (IsNuevo) { Detalles.Add(WirarLinea(detalle)); return true; }
        if (_session.Usuario is null) return false;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var dto = new CrearDetalleLineaDto(
                detalle.ProductoId, detalle.Descripcion, detalle.Cantidad, detalle.PrecioUnitario,
                detalle.DescuentoPct, detalle.IvaAplicable);
            var r = await _service.AgregarDetalleAsync(_documento!.Id, dto, _session.Usuario.Id);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo agregar."; return false; }
            Detalles.Add(WirarLinea(new DetalleLineaEditable(
                r.Value!.Id, r.Value.ProductoId, r.Value.Descripcion,
                r.Value.Cantidad, r.Value.PrecioUnitario,
                sku:          detalle.Sku,
                ivaAplicable: detalle.IvaAplicable,
                descuentoPct: r.Value.DescuentoPct)));
            RecalcularTotales();
            return true;
        }
        finally { IsBusy = false; }
    }

    private bool HayDetalle => DetalleSeleccionado is not null;

    /// <summary>
    /// Actualiza la cantidad de una línea — guard IsBusy (ADR-043) para evitar concurrencia DbContext.
    /// cantidad=0 elimina la línea.
    /// </summary>
    public async Task ActualizarCantidadAsync(DetalleLineaEditable linea, decimal nuevaCantidad)
    {
        if (nuevaCantidad < 0) return;
        if (IsBusy) return;   // Guard ADR-043
        // Guard anti-inicialización: evita delete+readd cuando NumberBox dispara NaN→valor en DataTemplate render
        if (nuevaCantidad == linea.Cantidad && !IsNuevo) return;

        if (nuevaCantidad == 0)
        {
            DetalleSeleccionado = linea;
            await EliminarDetalleCommand.ExecuteAsync(null);
            return;
        }

        if (IsNuevo) { linea.Cantidad = nuevaCantidad; return; }
        if (_session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var rEl = await _service.EliminarDetalleAsync(linea.Id, _session.Usuario.Id);
            if (!rEl.Success) { ErrorMessage = rEl.Error ?? "No se pudo actualizar."; return; }
            var dto = new CrearDetalleLineaDto(linea.ProductoId, linea.Descripcion, nuevaCantidad, linea.PrecioUnitario, linea.DescuentoPct, linea.IvaAplicable);
            var rAg = await _service.AgregarDetalleAsync(_documento!.Id, dto, _session.Usuario.Id);
            if (!rAg.Success) { ErrorMessage = rAg.Error ?? "No se pudo actualizar."; return; }
            linea.Id = rAg.Value!.Id;
            linea.Cantidad = nuevaCantidad;
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Actualiza el descuento de una línea individual — guard IsBusy (ADR-043).
    /// </summary>
    public async Task ActualizarDescuentoAsync(DetalleLineaEditable linea, decimal nuevoPct)
    {
        nuevoPct = Math.Clamp(nuevoPct, 0m, 100m);
        if (IsBusy) return;
        if (linea.DescuentoPct == nuevoPct) return;   // anti-reentrancy

        if (IsNuevo)
        {
            linea.DescuentoPct = nuevoPct;
            RecalcularTotales();
            return;
        }
        if (_session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var rEl = await _service.EliminarDetalleAsync(linea.Id, _session.Usuario.Id);
            if (!rEl.Success) { ErrorMessage = rEl.Error ?? "No se pudo actualizar descuento."; return; }
            var dto = new CrearDetalleLineaDto(linea.ProductoId, linea.Descripcion, linea.Cantidad, linea.PrecioUnitario, nuevoPct, linea.IvaAplicable);
            var rAg = await _service.AgregarDetalleAsync(_documento!.Id, dto, _session.Usuario.Id);
            if (!rAg.Success) { ErrorMessage = rAg.Error ?? "No se pudo actualizar descuento."; return; }
            linea.Id         = rAg.Value!.Id;
            linea.DescuentoPct = nuevoPct;
            RecalcularTotales();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(HayDetalle))]
    public async Task EliminarDetalleAsync(CancellationToken ct = default)
    {
        if (DetalleSeleccionado is null) return;
        if (IsNuevo) { Detalles.Remove(DetalleSeleccionado); DetalleSeleccionado = null; return; }
        if (_session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.EliminarDetalleAsync(DetalleSeleccionado.Id, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo eliminar."; return; }
            Detalles.Remove(DetalleSeleccionado); DetalleSeleccionado = null; RecalcularTotales();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task AvanzarEstatusAsync(CancellationToken ct = default)
    {
        if (_documento is null || _session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.CambiarEstatusAsync(_documento.Id, SiguienteEstatus, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo avanzar."; return; }
            Estatus = SiguienteEstatus; _documento = _documento with { Estatus = Estatus };
            SuccessMessage = $"Estado → {EstatusTextoDisplay}"; RefrescarPermisosUI();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task GenerarOrdenTrabajoAsync(string descripcion, CancellationToken ct = default)
    {
        if (_documento is null || _session.Usuario is null) return;
        if (string.IsNullOrWhiteSpace(descripcion)) { ErrorMessage = "La descripción del trabajo es obligatoria."; return; }
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.GenerarOrdenTrabajoAsync(_documento.Id, descripcion, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo generar la OT."; return; }
            SuccessMessage = $"OT #{r.Value!.Id} generada.";
            NotificarOTGenerada?.Invoke(r.Value);
        }
        finally { IsBusy = false; }
    }

    /// <summary>ID del documento guardado. Null si es nuevo.</summary>
    public long? DocumentoId => _documento?.Id;

    /// <summary>Cancela el pedido. Llamado desde la Page para el botón Cancelar.</summary>
    public async Task<bool> CancelarAsync(CancellationToken ct = default)
    {
        if (_documento is null || _session.Usuario is null) return false;
        var r = await _service.CambiarEstatusAsync(_documento.Id, EstatusPedido.Cancelado, _session.Usuario.Id, ct);
        if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo cancelar."; return false; }
        Estatus = EstatusPedido.Cancelado; _documento = _documento with { Estatus = Estatus };
        SuccessMessage = "Pedido cancelado."; RefrescarPermisosUI();
        return true;
    }

    private void RecalcularTotales()
    {
        SubtotalBruto    = Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);
        Subtotal         = Detalles.Sum(d => d.Importe);
        DescuentoTotal   = SubtotalBruto - Subtotal;
        TotalOtrosCargos = Cargos.Sum(c => c.Importe);

        var ivaProductos = CommercialDocumentCalculator.CalcularImpuestos(
            Detalles.Select(d => (d.Importe, d.IvaAplicable)), _tasaIva);
        var ivaCargos = CommercialDocumentCalculator.CalcularImpuestos(
            Cargos.Select(c => (c.Importe, c.AplicaIva)), _tasaIva);
        Impuestos = ivaProductos + ivaCargos;

        Total         = CommercialDocumentCalculator.CalcularTotal(Subtotal + TotalOtrosCargos, Impuestos);
        TotalArticulos = Detalles.Sum(d => d.Cantidad);
        OnPropertyChanged(nameof(HayDescuento));
        OnPropertyChanged(nameof(HayDescuentosEnLineas));
    }

    private DetalleLineaEditable WirarLinea(DetalleLineaEditable linea)
    {
        linea.ValorCambiadoCallback = () => { RecalcularTotales(); };
        return linea;
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
            cargo.Id = 0;
            Cargos.Add(cargo);
            return;
        }

        if (_session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var dto = new CrearPedidoCargoDto(cargo.Descripcion, cargo.Importe, cargo.AplicaIva, Cargos.Count);
            var r   = await _service.AgregarCargoAsync(_documento!.Id, dto, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo agregar el cargo."; return; }
            cargo.Id = r.Value!.Id;
            Cargos.Add(cargo);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task EliminarCargoAsync(CancellationToken ct = default)
    {
        if (CargoSeleccionado is null) return;

        if (IsNuevo || CargoSeleccionado.Id == 0)
        {
            Cargos.Remove(CargoSeleccionado);
            CargoSeleccionado = null;
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
        }
        finally { IsBusy = false; }
    }

    // ── Descuento global (ADR-042 — simplificado para Pedido) ────────────────

    public async Task AplicarDescuentoGlobalALineasAsync(decimal pct, CancellationToken ct = default)
    {
        if (IsBusy) return;
        pct = Math.Clamp(pct, 0m, 100m);

        if (IsNuevo)
        {
            foreach (var l in Detalles) l.DescuentoPct = pct;
            RecalcularTotales();
            return;
        }

        if (_session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            // Fase 1 — memoria
            foreach (var l in Detalles) l.DescuentoPct = pct;
            RecalcularTotales();

            // Fase 2 — BD: delete + readd por línea
            foreach (var linea in Detalles.ToList())
            {
                await _service.EliminarDetalleAsync(linea.Id, _session.Usuario.Id, ct);
                var dto = new CrearDetalleLineaDto(linea.ProductoId, linea.Descripcion, linea.Cantidad,
                    linea.PrecioUnitario, pct, linea.IvaAplicable);
                var r = await _service.AgregarDetalleAsync(_documento!.Id, dto, _session.Usuario.Id, ct);
                if (r.Success) linea.Id = r.Value!.Id;
            }
        }
        finally { IsBusy = false; }
    }

    private void RefrescarPermisosUI()
    {
        OnPropertyChanged(nameof(PuedeEditar));
        OnPropertyChanged(nameof(PuedeAvanzar));
        OnPropertyChanged(nameof(PuedeGenerarOT));
        OnPropertyChanged(nameof(PuedeCancelar));
        OnPropertyChanged(nameof(PuedeGenerarVenta));
        OnPropertyChanged(nameof(EstatusTextoDisplay));
        OnPropertyChanged(nameof(SiguienteEstatus));
        OnPropertyChanged(nameof(TituloDocumento));
        OnPropertyChanged(nameof(HayCargos));
        OnPropertyChanged(nameof(HayDescuento));
    }

    /// <summary>
    /// Entidad de Directorio actualmente seleccionada.
    /// Expuesta públicamente para que el rehost constructor restaure el chip del selector.
    /// </summary>
    public DirectorioSelectorDto? EntidadDirectorioSeleccionada => _entidadDirectorioSeleccionada;

    /// <summary>
    /// Restaura el chip del selector con el DTO completo (email, teléfono) post-hydration.
    /// NO marca IsDirty — es restauración de estado, no cambio del usuario.
    /// Mismo patrón que CotizacionDocumentoViewModel (Selector DTO Hydration Rule).
    /// </summary>
    public void RestaurarEntidadSeleccionada(DirectorioSelectorDto entidad)
    {
        _entidadDirectorioSeleccionada = entidad;
        NombreCliente   = entidad.DisplayName;
        ClienteEmail    = entidad.Email;
        ClienteTelefono = entidad.Telefono;
        OnPropertyChanged(nameof(EntidadDirectorioSeleccionada));
        OnPropertyChanged(nameof(TieneClienteSeleccionado));
    }

    /// <summary>
    /// Genera una Venta Documental desde este pedido, copiando cliente, detalles y observaciones.
    /// Valida que el pedido no esté cancelado. Abre la venta en el workspace vía callback.
    /// </summary>
    [RelayCommand]
    public async Task GenerarVentaAsync(CancellationToken ct = default)
    {
        if (_documento is null || _session.Usuario is null) return;
        if (Estatus == EstatusPedido.Cancelado) { ErrorMessage = "No se puede generar una venta desde un pedido cancelado."; return; }
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _ventaService.GenerarDesdePedidoAsync(_documento.Id, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo generar la venta."; return; }
            SuccessMessage = $"Venta #{r.Value!.Id} generada desde Pedido #{_documento.Id}.";
            NotificarVentaGenerada?.Invoke(r.Value);
        }
        finally { IsBusy = false; }
    }
}

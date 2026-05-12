using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Directorio;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
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
/// Fórmulas (ADR-040 — Operational Commercial Document Standard):
/// - DetalleLinea.Importe = Cantidad × PrecioUnitario (calculado, persistido en BD)
/// - Subtotal = SUM(Detalles.Importe) — runtime, se persiste en BD al guardar
/// - Impuestos = SUM(Importe × TasaIvaEstandar) para líneas con IvaAplicable = true
/// - Total = Subtotal + Impuestos
/// </remarks>
public sealed partial class CotizacionDocumentoViewModel : ObservableObject
{
    private readonly ICotizacionService               _service;
    private readonly IRelacionComercialService         _relacionComercialService;
    private readonly IErpAuthorizationService         _auth;
    private readonly SessionService                   _session;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;

    // El documento cargado (null si es nuevo)
    private CotizacionDto? _documento;
    // Entidad de Directorio seleccionada (ADR-038). RelacionComercialId se resuelve en GuardarAsync.
    private DirectorioSelectorDto? _entidadDirectorioSeleccionada;

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
    /// Subtotal = SUM(Detalles.Importe)
    /// Calculado runtime en el ViewModel; se persiste en BD al llamar Guardar.
    /// </summary>
    [ObservableProperty] private decimal subtotal;

    /// <summary>
    /// Total = Subtotal + Impuestos (ADR-040).
    /// Se persistirá en BD al llamar Guardar.
    /// </summary>
    [ObservableProperty] private decimal total;

    /// <summary>
    /// Impuestos = SUM(líneas con IVA) × TasaIvaEstandar (ADR-040).
    /// Calculado runtime; no persiste de forma independiente (el Total ya lo incluye).
    /// </summary>
    [ObservableProperty] private decimal impuestos;

    /// <summary>
    /// Indica si el documento tiene cambios no guardados (ADR-040).
    /// Se activa al agregar/eliminar líneas, cambiar cantidades, cambiar cliente u observaciones.
    /// Se reinicia a <c>false</c> tras guardar exitosamente.
    /// </summary>
    [ObservableProperty] private bool isDirty;

    // ── UI helpers ───────────────────────────────────────────────────────────
    public string TituloDocumento => IsNuevo ? "Nueva Cotización" : $"Cotización #{_documento?.Id}";
    public string EstatusTextoDisplay => Estatus switch
    {
        EstatusCotizacion.Borrador  => "Borrador",
        EstatusCotizacion.Enviada   => "Enviada",
        EstatusCotizacion.Aprobada  => "Aprobada",
        EstatusCotizacion.Cancelada => "Cancelada",
        _                           => Estatus.ToString()
    };
    public bool PuedeEditar       => Estatus != EstatusCotizacion.Cancelada;
    public bool PuedeEnviar       => Estatus == EstatusCotizacion.Borrador;
    public bool PuedeAprobar      => Estatus == EstatusCotizacion.Enviada;
    public bool PuedeConvertir    => Estatus == EstatusCotizacion.Aprobada && !IsNuevo;
    public bool PuedeCancelar     => Estatus is EstatusCotizacion.Borrador or EstatusCotizacion.Enviada && !IsNuevo;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EliminarDetalleCommand))]
    private DetalleLineaEditable? detalleSeleccionado;

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
        ICurrentContextTracker           contextTracker)
    {
        _service                   = service;
        _relacionComercialService  = relacionComercialService;
        _auth            = auth;
        _session         = session;
        _observability   = observability;
        _contextTracker  = contextTracker;
        Detalles.CollectionChanged += (_, _) => RecalcularTotales();
    }

    /// <summary>Inicializa el ViewModel con una cotización existente o deja en blanco para nueva.</summary>
    public void Initialize(CotizacionDto? cotizacion)
    {
        _documento = cotizacion;
        IsNuevo    = cotizacion is null;

        if (cotizacion is not null)
        {
            NombreCliente = cotizacion.NombreCliente;
            RelacionComercialId     = cotizacion.RelacionComercialId;
            Fecha         = cotizacion.Fecha;
            FechaVigencia = cotizacion.FechaVigencia;
            Observaciones = cotizacion.Observaciones;
            Estatus       = cotizacion.Estatus;

            Detalles.Clear();
            foreach (var d in cotizacion.Detalles)
                Detalles.Add(WirarLinea(new DetalleLineaEditable(d.Id, d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario)));
        }

        OnPropertyChanged(nameof(TieneClienteSeleccionado));
        RecalcularTotales();
        OnPropertyChanged(nameof(TituloDocumento));
        OnPropertyChanged(nameof(EstatusTextoDisplay));
        RefrescarPermisosUI();
        IsDirty = false;
    }

    /// <summary>
    /// Selecciona una entidad del Directorio (Persona o EmpresaComercial) desde el selector (ADR-038).
    /// RelacionComercialId se resolverá mediante GetOrCreate al guardar el documento.
    /// </summary>
    public void SeleccionarCliente(DirectorioSelectorDto? entidad)
    {
        _entidadDirectorioSeleccionada = entidad;
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

        // Validaciones básicas (§7 requerimiento)
        if (_entidadDirectorioSeleccionada is null && !RelacionComercialId.HasValue)
            { ErrorMessage = "Debe seleccionar un cliente del Directorio."; return; }
        if (string.IsNullOrWhiteSpace(NombreCliente)) { ErrorMessage = "El nombre del cliente es obligatorio."; return; }
        if (IsNuevo && Detalles.Count == 0) { ErrorMessage = "Debe agregar al menos una línea de detalle."; return; }

        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            // ADR-038: GetOrCreate RelacionComercial bajo demanda
            if (_entidadDirectorioSeleccionada is not null)
            {
                var rc = await _relacionComercialService.GetOrCreateAsync(
                    _session.EmpresaId, _entidadDirectorioSeleccionada, _session.Usuario!.Id, ct);
                if (!rc.Success) { ErrorMessage = rc.Error ?? "No se pudo vincular el cliente."; return; }
                RelacionComercialId = rc.Value;
            }
            if (IsNuevo)
            {
                // NEW document: create everything in one call
                var dto = new CrearCotizacionDto(
                    _session.EmpresaId, _session.SucursalId != 0 ? _session.SucursalId : null,
                    RelacionComercialId, NombreCliente, Fecha, FechaVigencia, Observaciones,
                    Detalles.Select(d => new CrearDetalleLineaDto(d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario)).ToList());

                var r = await _service.CrearAsync(dto, _session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo crear."; return; }
                SuccessMessage = $"Cotización #{r.Value!.Id} creada.";
                Initialize(r.Value);
                IsDirty = false;

                // Document Surface UX Pattern: notificar al módulo padre que el documento se guardó
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

                // Document Surface UX Pattern: notificar al módulo padre que el documento se guardó
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
        // For existing docs: calls service immediately
        // For new docs: the callback will add to local collection
        if (!IsNuevo && _documento is not null)
        {
            // Delegate to page via callback for dialog, then add via service
            // The page will provide SolicitarAgregarDetalle which shows a dialog
            // and returns the filled DetalleLineaEditable
            return;
        }
        // For new docs: the page will handle this via SolicitarAgregarDetalle callback
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
        if (nuevaCantidad < 0) return; // Negativo: ignorar silenciosamente

        if (nuevaCantidad == 0)
        {
            // Cantidad 0 → eliminar línea
            DetalleSeleccionado = linea;
            await EliminarDetalleCommand.ExecuteAsync(null);
            return;
        }

        if (IsNuevo)
        {
            linea.Cantidad = nuevaCantidad; // INPC notifica Importe automáticamente
            // IsDirty + RecalcularTotales se disparan via CantidadCambiadaCallback
        }
        else
        {
            if (_session.Usuario is null) return;
            IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
            try
            {
                var rEliminar = await _service.EliminarDetalleAsync(linea.Id, _session.Usuario.Id);
                if (!rEliminar.Success) { ErrorMessage = rEliminar.Error ?? "No se pudo actualizar la cantidad."; return; }

                var dto = new CrearDetalleLineaDto(linea.ProductoId, linea.Descripcion, nuevaCantidad, linea.PrecioUnitario);
                var rAgregar = await _service.AgregarDetalleAsync(_documento!.Id, dto, _session.Usuario.Id);
                if (!rAgregar.Success) { ErrorMessage = rAgregar.Error ?? "No se pudo actualizar la cantidad."; return; }

                linea.Id       = rAgregar.Value!.Id;
                linea.Cantidad = nuevaCantidad; // INPC notifica Importe + dispara callback
                IsDirty = true;
            }
            finally { IsBusy = false; }
        }
    }

    public async Task<bool> AgregarDetalleLocalAsync(DetalleLineaEditable detalle)
    {
        if (IsNuevo)
        {
            // New doc: add to local collection
            Detalles.Add(WirarLinea(detalle));
            IsDirty = true;
            return true;
        }
        else
        {
            // Existing doc: persist immediately
            if (_session.Usuario is null) return false;
            IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
            try
            {
                var dto = new CrearDetalleLineaDto(detalle.ProductoId, detalle.Descripcion, detalle.Cantidad, detalle.PrecioUnitario);
                var r   = await _service.AgregarDetalleAsync(_documento!.Id, dto, _session.Usuario.Id);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo agregar."; return false; }
                Detalles.Add(WirarLinea(new DetalleLineaEditable(r.Value!.Id, r.Value.ProductoId, r.Value.Descripcion, r.Value.Cantidad, r.Value.PrecioUnitario, ivaAplicable: detalle.IvaAplicable)));
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
    /// Conecta el callback de recálculo a la línea (ADR-041).
    /// Debe llamarse en TODA ruta que crea una <see cref="DetalleLineaEditable"/>.
    /// </summary>
    private DetalleLineaEditable WirarLinea(DetalleLineaEditable linea)
    {
        linea.CantidadCambiadaCallback = () =>
        {
            IsDirty = true;
            RecalcularTotales();
        };
        return linea;
    }

    /// <summary>
    /// Recalcula Subtotal, Impuestos y Total a partir de las líneas actuales (ADR-040).
    /// Centraliza toda la aritmética — NO duplicar en otros métodos.
    /// </summary>
    private void RecalcularTotales()
    {
        Subtotal   = CalcularSubtotal();
        Impuestos  = CalcularImpuestos();
        Total      = Subtotal + Impuestos;
    }

    /// <summary>Subtotal = SUM(Detalles.Importe).</summary>
    private decimal CalcularSubtotal() => Detalles.Sum(d => d.Importe);

    /// <summary>
    /// Impuestos = SUM(Importe × TasaIvaEstandar) para líneas con IvaAplicable = true.
    /// Usa <see cref="FiscalConstants.TasaIvaEstandar"/> — no hardcoding.
    /// </summary>
    private decimal CalcularImpuestos()
        => Detalles.Where(d => d.IvaAplicable).Sum(d => d.Importe) * FiscalConstants.TasaIvaEstandar;

    private void RefrescarPermisosUI()
    {
        OnPropertyChanged(nameof(PuedeEditar));
        OnPropertyChanged(nameof(PuedeEnviar));
        OnPropertyChanged(nameof(PuedeAprobar));
        OnPropertyChanged(nameof(PuedeConvertir));
        OnPropertyChanged(nameof(PuedeCancelar));
        OnPropertyChanged(nameof(EstatusTextoDisplay));
    }

    private void ReportarContexto()
    {
        // CategoriaFiltro se usa para reflejar cliente activo en el panel diagnóstico
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
/// Implementa <see cref="INotifyPropertyChanged"/> para que el ListView refleje cambios de cantidad
/// e importe sin necesidad de guardar (ADR-041 — Operational Editable Document Lines Pattern).
/// </summary>
/// <remarks>
/// Importe = Cantidad × PrecioUnitario — runtime, NO persistido directamente aquí.
/// Se persiste en BD cuando el servicio llama a SaveChangesAsync.
/// </remarks>
public sealed class DetalleLineaEditable : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    // ── Callback al ViewModel para recalcular totales al cambiar cantidad ──────
    /// <summary>
    /// Invocado cuando cambia la cantidad. El ViewModel lo conecta a RecalcularTotales().
    /// Permite que la edición inline en el ListView dispare el recálculo del documento.
    /// </summary>
    internal Action? CantidadCambiadaCallback { get; set; }

    public DetalleLineaEditable() { }

    /// <param name="id">0 para líneas nuevas no guardadas; &gt;0 para líneas ya persistidas.</param>
    /// <param name="sku">Código/SKU del producto. Solo informativo — no se persiste en detalle.</param>
    /// <param name="existenciaDisponible">
    /// Existencia total disponible en la empresa al momento de cargar el diálogo.
    /// Runtime — no se persiste ni reserva stock. Solo orientativo para el vendedor.
    /// </param>
    /// <param name="ivaAplicable">Si <c>true</c>, el producto tiene IVA estándar (ADR-040).</param>
    public DetalleLineaEditable(long id, int? productoId, string descripcion, decimal cantidad, decimal precioUnitario,
        string? sku = null, decimal? existenciaDisponible = null, bool ivaAplicable = false)
    {
        Id                   = id;
        ProductoId           = productoId;
        Descripcion          = descripcion;
        _cantidad            = cantidad;
        _precioUnitario      = precioUnitario;
        Sku                  = sku;
        ExistenciaDisponible = existenciaDisponible;
        IvaAplicable         = ivaAplicable;
    }

    public long    Id         { get; set; }
    public int?    ProductoId { get; set; }
    public string  Descripcion { get; set; } = string.Empty;

    // ── Cantidad con notificación INPC ────────────────────────────────────────

    private decimal _cantidad;

    /// <summary>
    /// Cantidad de la línea.
    /// Al cambiar notifica Cantidad, CantidadDouble e Importe, y dispara CantidadCambiadaCallback.
    /// </summary>
    public decimal Cantidad
    {
        get => _cantidad;
        set => SetCantidad(value);
    }

    /// <summary>
    /// Wrapper double de Cantidad para el NumberBox de WinUI (ADR-041).
    /// TwoWay binding convierte automáticamente entre double y decimal.
    /// </summary>
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
        CantidadCambiadaCallback?.Invoke();
    }

    // ── PrecioUnitario con notificación INPC ──────────────────────────────────

    private decimal _precioUnitario;

    /// <summary>Precio unitario. Al cambiar notifica PrecioUnitario e Importe.</summary>
    public decimal PrecioUnitario
    {
        get => _precioUnitario;
        set
        {
            if (_precioUnitario == value) return;
            _precioUnitario = value;
            OnPropertyChanged(nameof(PrecioUnitario));
            OnPropertyChanged(nameof(Importe));
        }
    }

    /// <summary>SKU/código del producto. Solo informativo en el grid — no se persiste en el detalle.</summary>
    public string? Sku { get; set; }

    /// <summary>
    /// Existencia disponible total de la empresa al momento de crear la línea.
    /// Runtime — no se reserva ni descuenta. Solo orientativo para el vendedor (§25 CLAUDE_RULES.md).
    /// </summary>
    public decimal? ExistenciaDisponible { get; set; }

    /// <summary>
    /// Indica si el producto tiene IVA estándar aplicable (ADR-040).
    /// Basado en <c>Producto.IvaAplicable</c> al momento de agregar la línea.
    /// </summary>
    public bool IvaAplicable { get; set; }

    /// <summary>Texto para columna IVA del grid ("Sí" / "No").</summary>
    public string IvaTexto => IvaAplicable ? "Sí" : "No";

    /// <summary>
    /// Importe = Cantidad × PrecioUnitario.
    /// Se notifica automáticamente cuando cambian Cantidad o PrecioUnitario.
    /// El servicio lo persiste en BD al guardar.
    /// </summary>
    public decimal Importe => _cantidad * _precioUnitario;
}

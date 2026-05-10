using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Application.Services.Venta;
using Ybridio.Domain.Ventas;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Diagnostic;

namespace Ybridio.WinUI.ViewModels.Ventas;

/// <summary>
/// ViewModel de documento para el formulario de Orden de Trabajo (nueva o existente).
/// Gestiona encabezado, materiales/servicios y estados operacionales de la OT.
/// </summary>
/// <remarks>
/// Fórmulas:
/// - Material.Importe = Cantidad × PrecioUnitario (runtime, persistido por servicio)
/// - OT.Total = SUM(Materiales.Importe) — runtime en VM; persistido en BD por OrdenTrabajoService
/// - EsUrgente = FechaCompromiso ≤ hoy+1 AND estatus activo — runtime, no persiste
/// </remarks>
public sealed partial class OrdenTrabajoDocumentoViewModel : ObservableObject
{
    private readonly IOrdenTrabajoService             _service;
    private readonly IErpAuthorizationService         _auth;
    private readonly SessionService                   _session;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;

    private OrdenTrabajoDto? _documento;

    [ObservableProperty] private bool                isNuevo = true;
    [ObservableProperty] private bool                isBusy;
    [ObservableProperty] private string              errorMessage   = string.Empty;
    [ObservableProperty] private string              successMessage = string.Empty;

    [ObservableProperty] private string              nombreCliente  = string.Empty;
    [ObservableProperty] private int?                clienteId;
    [ObservableProperty] private DateTime            fecha          = DateTime.Today;
    [ObservableProperty] private DateTime?           fechaCompromiso;

    /// <summary>Wrapper DateTimeOffset para el DatePicker de Fecha.</summary>
    public DateTimeOffset FechaOffset
    {
        get => new DateTimeOffset(Fecha);
        set => Fecha = value.DateTime;
    }

    /// <summary>Wrapper DateTimeOffset para el DatePicker de FechaCompromiso.</summary>
    public DateTimeOffset FechaCompromisoOffset
    {
        get => FechaCompromiso.HasValue ? new DateTimeOffset(FechaCompromiso.Value) : DateTimeOffset.Now.AddDays(7);
        set => FechaCompromiso = value.DateTime;
    }

    partial void OnFechaChanged(DateTime value)           => OnPropertyChanged(nameof(FechaOffset));
    partial void OnFechaCompromisoChanged(DateTime? value) => OnPropertyChanged(nameof(FechaCompromisoOffset));
    [ObservableProperty] private string              descripcion    = string.Empty;
    [ObservableProperty] private string?             observaciones;
    [ObservableProperty] private Guid?               responsableId;
    [ObservableProperty] private EstatusOrdenTrabajo estatus        = EstatusOrdenTrabajo.Nueva;

    public ObservableCollection<DetalleLineaEditable> Materiales { get; } = [];

    /// <summary>Total = SUM(Materiales.Importe). Runtime; persistido en BD por el servicio.</summary>
    [ObservableProperty] private decimal total;

    public string TituloDocumento => IsNuevo ? "Nueva OT" : $"OT #{_documento?.Id}";
    public string EstatusTextoDisplay => Estatus switch
    {
        EstatusOrdenTrabajo.Nueva             => "Nueva",
        EstatusOrdenTrabajo.EnProceso         => "En Proceso",
        EstatusOrdenTrabajo.EsperandoMaterial => "Esperando Material",
        EstatusOrdenTrabajo.Terminada         => "Terminada",
        EstatusOrdenTrabajo.Entregada         => "Entregada",
        EstatusOrdenTrabajo.Cancelada         => "Cancelada",
        _                                     => Estatus.ToString()
    };

    /// <summary>
    /// EsUrgente = FechaCompromiso ≤ hoy+1 Y estatus activo.
    /// Runtime — no se persiste porque depende de la fecha actual.
    /// </summary>
    public bool EsUrgente => FechaCompromiso.HasValue
        && FechaCompromiso.Value.Date <= DateTime.Today.AddDays(1)
        && Estatus is not (EstatusOrdenTrabajo.Entregada or EstatusOrdenTrabajo.Cancelada);

    public bool PuedeEditar   => Estatus != EstatusOrdenTrabajo.Cancelada;
    public bool PuedeAvanzar  => Estatus is EstatusOrdenTrabajo.Nueva or EstatusOrdenTrabajo.EnProceso or EstatusOrdenTrabajo.EsperandoMaterial;
    public bool PuedeCerrar   => Estatus == EstatusOrdenTrabajo.Terminada;
    public bool PuedeCancelar => Estatus is not (EstatusOrdenTrabajo.Entregada or EstatusOrdenTrabajo.Cancelada) && !IsNuevo;

    public EstatusOrdenTrabajo SiguienteEstatus => Estatus switch
    {
        EstatusOrdenTrabajo.Nueva             => EstatusOrdenTrabajo.EnProceso,
        EstatusOrdenTrabajo.EnProceso         => EstatusOrdenTrabajo.Terminada,
        EstatusOrdenTrabajo.EsperandoMaterial => EstatusOrdenTrabajo.EnProceso,
        EstatusOrdenTrabajo.Terminada         => EstatusOrdenTrabajo.Entregada,
        _                                     => Estatus
    };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EliminarMaterialCommand))]
    private DetalleLineaEditable? materialSeleccionado;

    public OrdenTrabajoDocumentoViewModel(
        IOrdenTrabajoService             service,
        IErpAuthorizationService         auth,
        SessionService                   session,
        IOperationalObservabilityService observability,
        ICurrentContextTracker           contextTracker)
    {
        _service        = service;
        _auth           = auth;
        _session        = session;
        _observability  = observability;
        _contextTracker = contextTracker;
        Materiales.CollectionChanged += (_, _) => RecalcularTotal();
    }

    public void Initialize(OrdenTrabajoDto? ot)
    {
        _documento = ot;
        IsNuevo    = ot is null;

        if (ot is not null)
        {
            NombreCliente  = ot.NombreCliente;
            ClienteId      = ot.ClienteId;
            Fecha          = ot.Fecha;
            FechaCompromiso= ot.FechaCompromiso;
            Descripcion    = ot.Descripcion;
            Observaciones  = ot.Observaciones;
            ResponsableId  = ot.ResponsableId;
            Estatus        = ot.Estatus;

            Materiales.Clear();
            foreach (var m in ot.Materiales)
                Materiales.Add(new DetalleLineaEditable(m.Id, m.ProductoId, m.Descripcion, m.Cantidad, m.PrecioUnitario));
        }

        RecalcularTotal();
        OnPropertyChanged(nameof(TituloDocumento));
        OnPropertyChanged(nameof(EsUrgente));
        RefrescarPermisosUI();
    }

    [RelayCommand]
    public async Task GuardarAsync(CancellationToken ct = default)
    {
        if (_session.Usuario is null) return;
        if (string.IsNullOrWhiteSpace(NombreCliente)) { ErrorMessage = "El nombre del cliente es obligatorio."; return; }
        if (string.IsNullOrWhiteSpace(Descripcion))   { ErrorMessage = "La descripción del trabajo es obligatoria."; return; }

        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            if (IsNuevo)
            {
                var dto = new CrearOrdenTrabajoDto(
                    _session.EmpresaId, _session.SucursalId != 0 ? _session.SucursalId : null,
                    ClienteId, NombreCliente, null, Fecha, FechaCompromiso, Descripcion, Observaciones, ResponsableId ?? _session.Usuario.Id);

                var r = await _service.CrearAsync(dto, _session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo crear."; return; }
                SuccessMessage = $"OT #{r.Value!.Id} creada.";
                Initialize(r.Value);
            }
            else
            {
                var dto = new ActualizarOrdenTrabajoDto(ClienteId, NombreCliente, Fecha, FechaCompromiso, Descripcion, Observaciones, ResponsableId);
                var r   = await _service.ActualizarAsync(_documento!.Id, dto, _session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo actualizar."; return; }
                SuccessMessage = "OT actualizada.";
                _documento = r.Value;
            }
        }
        finally { IsBusy = false; }
    }

    public async Task<bool> AgregarMaterialLocalAsync(DetalleLineaEditable mat)
    {
        if (IsNuevo) { Materiales.Add(mat); return true; }
        if (_session.Usuario is null) return false;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var dto = new AgregarOTMaterialDto(mat.ProductoId, mat.Descripcion, mat.Cantidad, mat.PrecioUnitario);
            var r   = await _service.AgregarMaterialAsync(_documento!.Id, dto, _session.Usuario.Id);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo agregar."; return false; }
            Materiales.Add(new DetalleLineaEditable(r.Value!.Id, r.Value.ProductoId, r.Value.Descripcion, r.Value.Cantidad, r.Value.PrecioUnitario));
            RecalcularTotal(); return true;
        }
        finally { IsBusy = false; }
    }

    private bool HayMaterial => MaterialSeleccionado is not null;

    [RelayCommand(CanExecute = nameof(HayMaterial))]
    public async Task EliminarMaterialAsync(CancellationToken ct = default)
    {
        if (MaterialSeleccionado is null) return;
        if (IsNuevo) { Materiales.Remove(MaterialSeleccionado); MaterialSeleccionado = null; return; }
        if (_session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.EliminarMaterialAsync(MaterialSeleccionado.Id, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo eliminar."; return; }
            Materiales.Remove(MaterialSeleccionado); MaterialSeleccionado = null; RecalcularTotal();
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
            SuccessMessage = $"Estado → {EstatusTextoDisplay}";
            OnPropertyChanged(nameof(EsUrgente)); RefrescarPermisosUI();
        }
        finally { IsBusy = false; }
    }

    /// <summary>ID del documento guardado. Null si es nuevo.</summary>
    public long? DocumentoId => _documento?.Id;

    /// <summary>Cancela la OT. Llamado desde la Page para el botón Cancelar.</summary>
    public async Task<bool> CancelarAsync(CancellationToken ct = default)
    {
        if (_documento is null || _session.Usuario is null) return false;
        var r = await _service.CambiarEstatusAsync(_documento.Id, EstatusOrdenTrabajo.Cancelada, _session.Usuario.Id, ct);
        if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo cancelar."; return false; }
        Estatus = EstatusOrdenTrabajo.Cancelada; _documento = _documento with { Estatus = Estatus };
        SuccessMessage = "OT cancelada."; RefrescarPermisosUI();
        return true;
    }

    private void RecalcularTotal() => Total = Materiales.Sum(m => m.Importe);

    private void RefrescarPermisosUI()
    {
        OnPropertyChanged(nameof(PuedeEditar)); OnPropertyChanged(nameof(PuedeAvanzar));
        OnPropertyChanged(nameof(PuedeCerrar)); OnPropertyChanged(nameof(PuedeCancelar));
        OnPropertyChanged(nameof(PuedeMarcarEntregada));
        OnPropertyChanged(nameof(EstatusTextoDisplay)); OnPropertyChanged(nameof(SiguienteEstatus));
    }

    /// <summary>
    /// True cuando la OT está Terminada y puede marcarse como Entregada.
    /// Validación de workflow: solo OTs terminadas pueden entregarse.
    /// </summary>
    public bool PuedeMarcarEntregada => Estatus == EstatusOrdenTrabajo.Terminada && !IsNuevo;

    /// <summary>
    /// Marca la OT como Entregada. Solo válido desde estado Terminada.
    /// Registra el timestamp de entrega vía servicio.
    /// </summary>
    [RelayCommand]
    public async Task MarcarEntregadaAsync(CancellationToken ct = default)
    {
        if (_documento is null || _session.Usuario is null) return;
        if (Estatus != EstatusOrdenTrabajo.Terminada) { ErrorMessage = "Solo se puede entregar una OT en estado Terminada."; return; }
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var r = await _service.CambiarEstatusAsync(_documento.Id, EstatusOrdenTrabajo.Entregada, _session.Usuario.Id, ct);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo marcar como entregada."; return; }
            Estatus = EstatusOrdenTrabajo.Entregada; _documento = _documento with { Estatus = Estatus };
            SuccessMessage = $"OT #{_documento.Id} marcada como Entregada — {DateTime.Now:dd/MM/yyyy HH:mm}";
            OnPropertyChanged(nameof(EsUrgente)); RefrescarPermisosUI();
        }
        finally { IsBusy = false; }
    }
}

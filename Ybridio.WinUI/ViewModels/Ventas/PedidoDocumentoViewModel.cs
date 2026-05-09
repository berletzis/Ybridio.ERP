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
/// ViewModel de documento para el formulario de pedido (nuevo o existente).
/// Gestiona encabezado, detalles y acciones de workflow (Generar OT, avanzar estado).
/// </summary>
/// <remarks>
/// Fórmulas:
/// - DetalleLinea.Importe = Cantidad × PrecioUnitario (runtime, persistido al guardar)
/// - Total = SUM(Detalles.Importe) (runtime, persistido al guardar)
/// </remarks>
public sealed partial class PedidoDocumentoViewModel : ObservableObject
{
    private readonly IPedidoService                   _service;
    private readonly IErpAuthorizationService         _auth;
    private readonly SessionService                   _session;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;

    private PedidoDto? _documento;

    [ObservableProperty] private bool          isNuevo = true;
    [ObservableProperty] private bool          isBusy;
    [ObservableProperty] private string        errorMessage   = string.Empty;
    [ObservableProperty] private string        successMessage = string.Empty;

    [ObservableProperty] private string        nombreCliente  = string.Empty;
    [ObservableProperty] private int?          clienteId;
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
    [ObservableProperty] private EstatusPedido estatus        = EstatusPedido.Nuevo;

    public ObservableCollection<DetalleLineaEditable> Detalles { get; } = [];

    /// <summary>Total = SUM(Detalles.Importe). Runtime; persistido en BD al guardar.</summary>
    [ObservableProperty] private decimal total;

    public string TituloDocumento => IsNuevo ? "Nuevo Pedido" : $"Pedido #{_documento?.Id}";
    public string EstatusTextoDisplay => Estatus switch
    {
        EstatusPedido.Nuevo      => "Nuevo",
        EstatusPedido.Confirmado => "Confirmado",
        EstatusPedido.EnProceso  => "En Proceso",
        EstatusPedido.Completado => "Completado",
        EstatusPedido.Cancelado  => "Cancelado",
        _                        => Estatus.ToString()
    };
    public bool PuedeEditar    => Estatus != EstatusPedido.Cancelado;
    public bool PuedeAvanzar   => Estatus is EstatusPedido.Nuevo or EstatusPedido.Confirmado or EstatusPedido.EnProceso;
    public bool PuedeGenerarOT => Estatus is EstatusPedido.Confirmado or EstatusPedido.EnProceso && !IsNuevo;
    public bool PuedeCancelar  => Estatus is not (EstatusPedido.Completado or EstatusPedido.Cancelado) && !IsNuevo;

    public EstatusPedido SiguienteEstatus => Estatus switch
    {
        EstatusPedido.Nuevo      => EstatusPedido.Confirmado,
        EstatusPedido.Confirmado => EstatusPedido.EnProceso,
        EstatusPedido.EnProceso  => EstatusPedido.Completado,
        _                        => Estatus
    };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EliminarDetalleCommand))]
    private DetalleLineaEditable? detalleSeleccionado;

    public Action<OrdenTrabajoDto>? NotificarOTGenerada;

    public PedidoDocumentoViewModel(
        IPedidoService                   service,
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
        Detalles.CollectionChanged += (_, _) => RecalcularTotales();
    }

    public void Initialize(PedidoDto? pedido)
    {
        _documento = pedido;
        IsNuevo    = pedido is null;

        if (pedido is not null)
        {
            NombreCliente          = pedido.NombreCliente;
            ClienteId              = pedido.ClienteId;
            Fecha                  = pedido.Fecha;
            FechaEntregaCompromiso = pedido.FechaEntregaCompromiso;
            Observaciones          = pedido.Observaciones;
            Estatus                = pedido.Estatus;

            Detalles.Clear();
            foreach (var d in pedido.Detalles)
                Detalles.Add(new DetalleLineaEditable(d.Id, d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario));
        }

        RecalcularTotales();
        OnPropertyChanged(nameof(TituloDocumento));
        RefrescarPermisosUI();
    }

    [RelayCommand]
    public async Task GuardarAsync(CancellationToken ct = default)
    {
        if (_session.Usuario is null) return;
        if (string.IsNullOrWhiteSpace(NombreCliente)) { ErrorMessage = "El nombre del cliente es obligatorio."; return; }

        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            if (IsNuevo)
            {
                var dto = new CrearPedidoDto(
                    _session.EmpresaId, _session.SucursalId != 0 ? _session.SucursalId : null,
                    ClienteId, NombreCliente, null, Fecha, FechaEntregaCompromiso, Observaciones,
                    Detalles.Select(d => new CrearDetalleLineaDto(d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario)).ToList());

                var r = await _service.CrearAsync(dto, _session.Usuario.Id, ct);
                if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo crear."; return; }
                SuccessMessage = $"Pedido #{r.Value!.Id} creado.";
                Initialize(r.Value);
            }
            else
            {
                var dto = new ActualizarPedidoDto(ClienteId, NombreCliente, Fecha, FechaEntregaCompromiso, Observaciones);
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
        if (IsNuevo) { Detalles.Add(detalle); return true; }
        if (_session.Usuario is null) return false;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var dto = new CrearDetalleLineaDto(detalle.ProductoId, detalle.Descripcion, detalle.Cantidad, detalle.PrecioUnitario);
            var r   = await _service.AgregarDetalleAsync(_documento!.Id, dto, _session.Usuario.Id);
            if (!r.Success) { ErrorMessage = r.Error ?? "No se pudo agregar."; return false; }
            Detalles.Add(new DetalleLineaEditable(r.Value!.Id, r.Value.ProductoId, r.Value.Descripcion, r.Value.Cantidad, r.Value.PrecioUnitario));
            RecalcularTotales();
            return true;
        }
        finally { IsBusy = false; }
    }

    private bool HayDetalle => DetalleSeleccionado is not null;

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

    private void RecalcularTotales() => Total = Detalles.Sum(d => d.Importe);

    private void RefrescarPermisosUI()
    {
        OnPropertyChanged(nameof(PuedeEditar)); OnPropertyChanged(nameof(PuedeAvanzar));
        OnPropertyChanged(nameof(PuedeGenerarOT)); OnPropertyChanged(nameof(PuedeCancelar));
        OnPropertyChanged(nameof(EstatusTextoDisplay)); OnPropertyChanged(nameof(SiguienteEstatus));
    }
}

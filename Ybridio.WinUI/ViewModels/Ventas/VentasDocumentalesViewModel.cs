using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Application.Services.Venta;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels;

namespace Ybridio.WinUI.ViewModels.Ventas;

/// <summary>ViewModel del sub-módulo Ventas > Ventas (lista de ventas documentales).</summary>
public sealed partial class VentasDocumentalesViewModel : BaseContextViewModel
{
    private readonly IVentaDocumentalService          _service;
    private readonly IErpAuthorizationService         _auth;
    private readonly IOperationalObservabilityService _observability;
    private readonly ICurrentContextTracker           _contextTracker;

    [ObservableProperty] private bool                         _isBusy;
    [ObservableProperty] private string                       _errorMessage   = string.Empty;
    [ObservableProperty] private string                       _successMessage = string.Empty;
    [ObservableProperty] private string                       _busqueda       = string.Empty;
    [ObservableProperty] private string                       _filtroTemporal = "30 dias";
    [ObservableProperty] private VentaDocumentalResumenDto?   _ventaSeleccionada;

    public ObservableCollection<VentaDocumentalResumenDto> Ventas { get; } = [];

    public VentasDocumentalesViewModel(
        IVentaDocumentalService          service,
        IErpAuthorizationService         auth,
        SessionService                   session,
        IOperationalObservabilityService observability,
        ICurrentContextTracker           contextTracker)
        : base(session)
    {
        _service        = service;
        _auth           = auth;
        _observability  = observability;
        _contextTracker = contextTracker;
    }

    protected override Task OnContextChangedAsync() => RefrescarAsync();

    /// <summary>Recarga la lista de ventas documentales según el filtro temporal activo.</summary>
    [RelayCommand]
    public async Task RefrescarAsync(CancellationToken ct = default)
    {
        if (Session.Usuario is null) return;
        IsBusy = true; ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            var dias = FiltroTemporal switch
            {
                "7 dias"  =>  7,
                "90 dias" => 90,
                "6 meses" => 180,
                "1 ano"   => 365,
                "Todo"    => 3650,
                _         => 30,
            };
            var desde = DateTime.Today.AddDays(-dias);
            var r = await _service.ListarAsync(Session.EmpresaId, desde, null, ct);
            Ventas.Clear();
            if (!r.Success) { ErrorMessage = r.Error ?? "Error al cargar."; return; }
            var filtradas = string.IsNullOrWhiteSpace(Busqueda)
                ? r.Value!
                : r.Value!.Where(v => v.NombreCliente.Contains(Busqueda, StringComparison.OrdinalIgnoreCase)
                                   || v.EstatusTexto.Contains(Busqueda, StringComparison.OrdinalIgnoreCase));
            foreach (var v in filtradas)
                Ventas.Add(v);

            ReportarContexto();
        }
        finally { IsBusy = false; }
    }

    private void ReportarContexto()
    {
        _contextTracker.SetViewModelContext(new CurrentOperationalContext(
            Module:          "Ventas",
            SubModule:       "Ventas",
            ViewModel:       nameof(VentasDocumentalesViewModel),
            Entity:          "Venta",
            RecordCount:     Ventas.Count,
            SearchTerm:      Busqueda,
            SoloActivos:     false,
            CategoriaFiltro: null,
            FiltroTemporal:  FiltroTemporal,
            EmpresaFilter:   null,
            SucursalFilter:  null,
            AlmacenFilter:   null,
            SoftDeleteFilter: null,
            Source:          "WorkspaceTab",
            UpdatedAt:       DateTime.Now));
    }

    /// <summary>Reporta el contexto vivo (llamado por la Page al revisitar el tab).</summary>
    public void ReportLiveContext() => ReportarContexto();
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Core;
using Ybridio.Application.Services.Caja;
using Ybridio.Application.Services.Sucursal;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Workspace;
using Ybridio.WinUI.Views;

namespace Ybridio.WinUI.ViewModels;

/// <summary>
/// ViewModel del Shell principal. Gestiona el TopBar, el selector de sucursal,
/// la sección activa y el Logout.
/// La navegación entre módulos (ModuleFrame) es responsabilidad de ShellPage.
/// WorkspaceService queda disponible para ítems de trabajo (Productos, registros específicos).
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly SessionService _session;
    private readonly ICajaService _caja;
    private readonly ISucursalService _sucursalService;
    private readonly IWorkspaceService _workspace;

    /// <summary>Sucursales disponibles para el usuario actual. Alimenta el selector del header.</summary>
    public ObservableCollection<SucursalDto> SucursalesDisponibles { get; } = [];

    // ── TopBar ───────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string usuarioNombre = string.Empty;

    [ObservableProperty]
    private string sucursalNombre = string.Empty;

    [ObservableProperty]
    private string cajaEstado = "Sin caja";

    [ObservableProperty]
    private bool isCajaActiva;

    [ObservableProperty]
    private string seccionActiva = "Ybridio ERP";

    public ShellViewModel(
        INavigationService navigation,
        SessionService session,
        ICajaService caja,
        ISucursalService sucursalService,
        IWorkspaceService workspace)
    {
        _navigation      = navigation;
        _session         = session;
        _caja            = caja;
        _sucursalService = sucursalService;
        _workspace       = workspace;
    }

    /// <summary>
    /// Inicializa el Shell: carga datos del usuario, sucursales disponibles y estado de caja.
    /// La vista inicial (DashboardPage en ModuleFrame) se establece desde ShellPage.OnNavigatedTo.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        UsuarioNombre  = _session.Usuario?.Nombre ?? "—";
        SucursalNombre = _session.SucursalNombre.Length > 0 ? _session.SucursalNombre : "Sin sucursal";

        if (_session.Usuario is not null)
        {
            var sucursales = await _sucursalService.ListarPorUsuarioAsync(_session.Usuario.Id, ct);
            SucursalesDisponibles.Clear();
            foreach (var t in sucursales) SucursalesDisponibles.Add(t);
        }

        await RefreshCajaAsync(ct);
    }

    public async Task RefreshCajaAsync(CancellationToken ct = default)
    {
        if (_session.Usuario is null) return;

        var result = await _caja.ObtenerCajaActivaAsync(_session.Usuario.Id, ct);
        if (result.Success && result.Value is not null)
        {
            _session.SetCajaActiva(result.Value);
            CajaEstado = result.Value.CajaNombre;
            IsCajaActiva = true;
        }
        else
        {
            _session.SetCajaActiva(null);
            CajaEstado = "Sin caja";
            IsCajaActiva = false;
        }
    }

    // ── Selección de módulo principal (sidebar) ───────────────────────────────
    // Solo actualiza SeccionActiva. La navegación real (ModuleFrame) ocurre en ShellPage.

    [RelayCommand]
    private void SelectModule(string modulo)
    {
        SeccionActiva = modulo switch
        {
            "Dashboard"                              => "Dashboard",
            "POS"                                    => "Punto de Venta",
            "Inventario"                             => "Inventario",
            "Ventas"                                 => "Ventas",
            "Contactos"                              => "Contactos",
            "Finanzas"                               => "Finanzas",
            "Configuracion" or "ConfiguracionGlobal" => "Configuración",
            "ConfiguracionTienda"                    => "Config. Tienda",
            _                                        => SeccionActiva
        };
    }

    // ── Navegación a sub-módulo (ribbon) ─────────────────────────────────────

    [RelayCommand]
    private void NavigateTo(string tag)
    {
        switch (tag)
        {
            case "POS":
                SelectModule("POS");
                break;

            case "Dashboard":
                SelectModule("Dashboard");
                break;

            // Sub-módulos pendientes — abrirán workspace tabs cuando se implementen
            case "AperturaCaja":
            case "CierreCaja":
            case "RetiroEfectivo":
            case "Cancelaciones":
            case "Devoluciones":
            case "ReimprimirTicket":
            case "ConsultaCierres":
            case "Productos":
            case "Existencias":
            case "Entradas":
            case "Salidas":
            case "Kardex":
            case "Conteo":
            case "OrdenesCompra":
            case "Cotizaciones":
            case "OrdenTrabajo":
            case "ConsultaVentas":
            case "Contactos":
            case "ConfigLocal":
            case "Impresoras":
            case "Cajas":
            case "Empresas":
            case "Usuarios":
            case "Catalogos":
                break;
        }
    }

    // ── Selector de sucursal ─────────────────────────────────────────────────

    [RelayCommand]
    private void SeleccionarSucursal(SucursalDto sucursal)
    {
        _session.SetTienda(sucursal.Id, sucursal.Nombre);
        SucursalNombre = sucursal.Nombre;
    }

    // ── Logout ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private void Logout()
    {
        _workspace.CloseAll();
        _session.Clear();
        _navigation.NavigateTo(typeof(LoginPage));
    }
}

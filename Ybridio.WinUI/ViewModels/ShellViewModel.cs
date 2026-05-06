using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Core;
using Ybridio.Application.Services.Caja;
using Ybridio.Application.Services.Sucursal;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Views;
using Ybridio.WinUI.Views.Configuracion;
using Ybridio.WinUI.Views.Contactos;
using Ybridio.WinUI.Views.Dashboard;
using Ybridio.WinUI.Views.Inventario;
using Ybridio.WinUI.Views.POS;
using Ybridio.WinUI.Views.Ventas;

namespace Ybridio.WinUI.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly SessionService _session;
    private readonly ICajaService _caja;
    private readonly ISucursalService _sucursalService;

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
        ISucursalService sucursalService)
    {
        _navigation      = navigation;
        _session         = session;
        _caja            = caja;
        _sucursalService = sucursalService;
    }

    public async Task InitializeAsync(Microsoft.UI.Xaml.Controls.Frame innerFrame, CancellationToken ct = default)
    {
        _navigation.Frame = innerFrame;

        UsuarioNombre  = _session.Usuario?.Nombre ?? "—";
        SucursalNombre = _session.SucursalNombre.Length > 0 ? _session.SucursalNombre : "Sin sucursal";

        // Cargar sucursales disponibles para el selector del header
        if (_session.Usuario is not null)
        {
            var sucursales = await _sucursalService.ListarPorUsuarioAsync(_session.Usuario.Id, ct);
            SucursalesDisponibles.Clear();
            foreach (var t in sucursales) SucursalesDisponibles.Add(t);
        }

        await RefreshCajaAsync(ct);

        _navigation.NavigateTo(typeof(DashboardPage));
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

    // ── Selección de módulo principal (barra izquierda) ──────────────────────

    [RelayCommand]
    private void SelectModule(string modulo)
    {
        switch (modulo)
        {
            case "Dashboard":
                SeccionActiva = "Dashboard";
                _navigation.NavigateTo(typeof(DashboardPage));
                break;

            case "POS":
                SeccionActiva = "Punto de Venta";
                _navigation.NavigateTo(typeof(PosPage));
                break;

            case "Inventario":
                SeccionActiva = "Inventario";
                _navigation.NavigateTo(typeof(InventarioPage));
                break;

            case "Ventas":
                SeccionActiva = "Ventas";
                _navigation.NavigateTo(typeof(VentasPage));
                break;

            case "Contactos":
                SeccionActiva = "Contactos";
                _navigation.NavigateTo(typeof(ContactosPage));
                break;

            case "Configuracion":
            case "ConfiguracionGlobal":
                SeccionActiva = "Configuración";
                _navigation.NavigateTo(typeof(ConfiguracionPage), "Global");
                break;

            case "ConfiguracionTienda":
                SeccionActiva = "Config. Tienda";
                _navigation.NavigateTo(typeof(ConfiguracionPage), "Tienda");
                break;
        }
    }

    // ── Navegación a sub-módulo (ribbon) ─────────────────────────────────────

    [RelayCommand]
    private void NavigateTo(string tag)
    {
        switch (tag)
        {
            case "POS":
                _navigation.NavigateTo(typeof(PosPage));
                break;

            case "Dashboard":
                _navigation.NavigateTo(typeof(DashboardPage));
                break;

            // Sub-módulos POS — páginas pendientes de implementar
            case "AperturaCaja":
            case "CierreCaja":
            case "RetiroEfectivo":
            case "Cancelaciones":
            case "Devoluciones":
            case "ReimprimirTicket":
            case "ConsultaCierres":
            // Sub-módulos Inventario
            case "Productos":
            case "Existencias":
            case "Entradas":
            case "Salidas":
            case "Kardex":
            case "Conteo":
            case "OrdenesCompra":
            // Sub-módulos Ventas
            case "Cotizaciones":
            case "OrdenTrabajo":
            case "ConsultaVentas":
            // Contactos
            case "Contactos":
            // Configuración
            case "ConfigLocal":
            case "Impresoras":
            case "Cajas":
            case "Empresas":
            case "Usuarios":
            case "Catalogos":
                // TODO: navegar a la página correspondiente cuando esté implementada
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
        _session.Clear();
        _navigation.NavigateTo(typeof(LoginPage));
    }
}
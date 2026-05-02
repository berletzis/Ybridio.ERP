using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.Services.Caja;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Views;
using Ybridio.WinUI.Views.Dashboard;
using Ybridio.WinUI.Views.Inventario;

using Ybridio.WinUI.Views.POS;

namespace Ybridio.WinUI.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly SessionService _session;
    private readonly ICajaService _caja;

    // ── TopBar ───────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string usuarioNombre = string.Empty;

    [ObservableProperty]
    private string tiendaNombre = string.Empty;

    [ObservableProperty]
    private string cajaEstado = "Sin caja";

    [ObservableProperty]
    private bool isCajaActiva;

    [ObservableProperty]
    private string seccionActiva = "Ybridio ERP";

    // ── Ribbon visibility por sección ────────────────────────────────────────

    [ObservableProperty]
    private Visibility showRibbonPOS = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility showRibbonInventario = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility showRibbonVentas = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility showRibbonContactos = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility showRibbonConfiguracion = Visibility.Collapsed;

    public ShellViewModel(INavigationService navigation, SessionService session, ICajaService caja)
    {
        _navigation = navigation;
        _session = session;
        _caja = caja;
    }

    public async Task InitializeAsync(Microsoft.UI.Xaml.Controls.Frame innerFrame, CancellationToken ct = default)
    {
        _navigation.Frame = innerFrame;

        UsuarioNombre = _session.Usuario?.Nombre ?? "—";
        TiendaNombre = _session.TiendaNombre.Length > 0 ? _session.TiendaNombre : "Sin tienda";

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
        // Colapsar todos los ribbons
        ShowRibbonPOS = Visibility.Collapsed;
        ShowRibbonInventario = Visibility.Collapsed;
        ShowRibbonVentas = Visibility.Collapsed;
        ShowRibbonContactos = Visibility.Collapsed;
        ShowRibbonConfiguracion = Visibility.Collapsed;

        switch (modulo)
        {
            case "Dashboard":
                SeccionActiva = "Dashboard";
                _navigation.NavigateTo(typeof(DashboardPage));
                break;

            case "POS":
                SeccionActiva = "Punto de Venta";
                ShowRibbonPOS = Visibility.Visible;
                _navigation.NavigateTo(typeof(PosPage));
                break;

            case "Inventario":
                SeccionActiva = "Inventario";
                ShowRibbonInventario = Visibility.Collapsed;
                _navigation.NavigateTo(typeof(InventarioPage));
                break;

            case "Ventas":
                SeccionActiva = "Ventas";
                ShowRibbonVentas = Visibility.Visible;
                break;

            case "Contactos":
                SeccionActiva = "Contactos";
                ShowRibbonContactos = Visibility.Visible;
                break;

            case "Configuracion":
                SeccionActiva = "Configuración";
                ShowRibbonConfiguracion = Visibility.Visible;
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

    // ── Logout ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private void Logout()
    {
        _session.Clear();
        _navigation.NavigateTo(typeof(LoginPage));
    }
}
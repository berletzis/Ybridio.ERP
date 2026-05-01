using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ybridio.Application.Services.Caja;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Views;
using Ybridio.WinUI.Views.Dashboard;
using Ybridio.WinUI.Views.POS;

namespace Ybridio.WinUI.ViewModels;

/// <summary>
/// ViewModel del shell principal. Controla la NavigationView lateral,
/// la TopBar y el CommandBar contextual.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly SessionService _session;
    private readonly ICajaService _caja;

    // ── TopBar ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string usuarioNombre = string.Empty;

    [ObservableProperty]
    private string tiendaNombre = string.Empty;

    [ObservableProperty]
    private string cajaEstado = "Sin caja";

    [ObservableProperty]
    private bool isCajaActiva;

    // ── Inner Frame ──────────────────────────────────────────────────────────

    [ObservableProperty]
    private string selectedMenuTag = "Dashboard";

    // CommandBar contextual
    [ObservableProperty]
    private bool showPosCommands;

    [ObservableProperty]
    private bool showInventoryCommands;

    [ObservableProperty]
    private bool showCajaCommands;

    public ShellViewModel(INavigationService navigation, SessionService session, ICajaService caja)
    {
        _navigation = navigation;
        _session = session;
        _caja = caja;
    }

    /// <summary>
    /// Debe llamarse cuando el ShellPage esté cargado para inicializar el estado.
    /// </summary>
    public async Task InitializeAsync(Microsoft.UI.Xaml.Controls.Frame innerFrame, CancellationToken ct = default)
    {
        // Exponer el frame interno al NavigationService
        _navigation.Frame = innerFrame;

        // Cargar datos de sesión en TopBar
        UsuarioNombre = _session.Usuario?.Nombre ?? "—";
        TiendaNombre = _session.TiendaNombre.Length > 0 ? _session.TiendaNombre : "Sin tienda";

        await RefreshCajaAsync(ct);

        // Navegar al Dashboard
        _navigation.NavigateTo(typeof(DashboardPage));
    }

    public async Task RefreshCajaAsync(CancellationToken ct = default)
    {
        if (_session.Usuario is null) return;

        var result = await _caja.ObtenerCajaActivaAsync(_session.Usuario.Id, ct);
        if (result.Succeeded && result.Value is not null)
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

    /// <summary>
    /// Llamado cuando el usuario selecciona un ítem en la NavigationView.
    /// </summary>
    [RelayCommand]
    private void NavigateTo(string tag)
    {
        SelectedMenuTag = tag;

        // Limpiar flags de comandos contextuales
        ShowPosCommands = false;
        ShowInventoryCommands = false;
        ShowCajaCommands = false;

        switch (tag)
        {
            case "Dashboard":
                _navigation.NavigateTo(typeof(DashboardPage));
                break;

            case "POS":
                _navigation.NavigateTo(typeof(PosPage));
                ShowPosCommands = true;
                break;

            case "AperturaCaja":
            case "CierreCaja":
            case "RetiroEfectivo":
            case "Cancelaciones":
            case "Devoluciones":
            case "ReimprimirTicket":
            case "ConsultaCierres":
                ShowCajaCommands = true;
                break;

            case "Entradas":
            case "Salidas":
            case "Kardex":
            case "Existencias":
            case "Conteo":
            case "OrdenesCompra":
            case "Productos":
                ShowInventoryCommands = true;
                break;
        }
    }

    [RelayCommand]
    private void Logout()
    {
        _session.Clear();
        _navigation.NavigateTo(typeof(LoginPage));
    }
}

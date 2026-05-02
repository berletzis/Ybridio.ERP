using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Ybridio.Application.Extensions;
using Ybridio.Infrastructure.Persistence;
using Ybridio.Infrastructure.Persistence.Identity;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.ViewModels;
using Ybridio.WinUI.ViewModels.Dashboard;
using Ybridio.WinUI.ViewModels.Inventario;
using Ybridio.WinUI.ViewModels.POS;
using Ybridio.WinUI.Views;
using Ybridio.WinUI.Views.Configuracion;
using Ybridio.WinUI.Views.Contactos;
using Ybridio.WinUI.Views.Dashboard;
using Ybridio.WinUI.Views.Inventario;
using Ybridio.WinUI.Views.POS;
using Ybridio.WinUI.Views.Ventas;
using System;

namespace Ybridio.WinUI;

public partial class App : Microsoft.UI.Xaml.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        window.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // ── Logging ───────────────────────────────────────────────────────────
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // ── EF Core + Identity ────────────────────────────────────────────────
        var connectionString =
            "Server=132.148.74.136\\ybridio;Database=YBRIDIO-26;user id=sa;password=U3xc3pt!0n!22;TrustServerCertificate=True;MultipleActiveResultSets=true";

        services.AddDbContext<ErpDbContext>(opt =>
            opt.UseSqlServer(connectionString), ServiceLifetime.Scoped);

        services.AddIdentityCore<ApplicationUser>()
                .AddRoles<ApplicationRole>()
                .AddEntityFrameworkStores<ErpDbContext>();

        // ── Application services ──────────────────────────────────────────────
        services.AddApplicationServices();

        // ── UI Services ───────────────────────────────────────────────────────
        services.AddSingleton<SessionService>();
        services.AddSingleton<INavigationService, NavigationService>();

        // ── ViewModels ────────────────────────────────────────────────────────
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<PosViewModel>();
        services.AddTransient<ProductosViewModel>();

        // ── Pages ─────────────────────────────────────────────────────────────
        services.AddTransient<LoginPage>();
        services.AddTransient<ShellPage>();
        services.AddTransient<DashboardPage>();
        services.AddTransient<PosPage>();

        // ── Módulos principales ───────────────────────────────────────────────
        services.AddTransient<VentasPage>();
        services.AddTransient<ContactosPage>();
        services.AddTransient<ConfiguracionPage>();

        // ── Inventario ────────────────────────────────────────────────────────
        services.AddTransient<InventarioPage>();
        services.AddTransient<ExistenciasPage>();
        services.AddTransient<EntradasPage>();
        services.AddTransient<SalidasPage>();
        services.AddTransient<KardexPage>();
        services.AddTransient<ConteoPage>();
        services.AddTransient<OrdenesCompraPage>();
        services.AddTransient<ProductosPage>();

        return services.BuildServiceProvider();
    }
}
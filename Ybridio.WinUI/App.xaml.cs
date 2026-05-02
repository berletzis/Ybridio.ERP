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
using Ybridio.WinUI.ViewModels.POS;
using Ybridio.WinUI.Views;
using Ybridio.WinUI.Views.Dashboard;
using Ybridio.WinUI.Views.POS;
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
        // La cadena de conexión debe configurarse para producción (appsettings, env-var, etc.)
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

        // ── Pages ─────────────────────────────────────────────────────────────
        services.AddTransient<LoginPage>();
        services.AddTransient<ShellPage>();
        services.AddTransient<DashboardPage>();
        services.AddTransient<PosPage>();

        return services.BuildServiceProvider();
    }
}

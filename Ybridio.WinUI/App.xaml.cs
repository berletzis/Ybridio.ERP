using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Ybridio.Application.Extensions;
using Ybridio.Infrastructure.Persistence;
using Ybridio.Infrastructure.Persistence.Audit;
using Ybridio.Infrastructure.Persistence.Identity;
using Ybridio.WinUI.Services;
using Ybridio.WinUI.Services.Windowing;
using Ybridio.WinUI.Services.Workspace;
using Ybridio.WinUI.ViewModels;
using Ybridio.Application.Services.Catalogos;
using Ybridio.Application.Services.Configuracion;
using Ybridio.WinUI.ViewModels.Config;
using Ybridio.WinUI.ViewModels.Dashboard;
using Ybridio.WinUI.Services.Diagnostic;
using Ybridio.WinUI.ViewModels.Diagnostic;
using Ybridio.WinUI.ViewModels.Finanzas;
using Ybridio.WinUI.ViewModels.Inventario;
using Ybridio.WinUI.ViewModels.Ventas;
using Ybridio.WinUI.ViewModels.POS;
using Ybridio.WinUI.Views;
using Ybridio.WinUI.Views.Configuracion;
using Ybridio.WinUI.Views.Contactos;
using Ybridio.WinUI.Views.Dashboard;
using Ybridio.WinUI.Views.Inventario;
using Ybridio.WinUI.Views.POS;
using Ybridio.WinUI.Views.Config;
using Ybridio.WinUI.Views.Finanzas;
using Ybridio.WinUI.Views.Ventas;
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
        var window = Services.GetRequiredService<MainWindow>();
        window.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // ── Logging ───────────────────────────────────────────────────────────
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // ── Configuración (connection string externalizado — Fase 1 Y26) ─────────
        // appsettings.json = plantilla sin credenciales (en repositorio)
        // appsettings.Development.json = credenciales reales (en .gitignore)
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // ── EF Core + Identity ────────────────────────────────────────────────
        var connectionString = config.GetConnectionString("ErpDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'ErpDatabase' no configurada. " +
                "Crea appsettings.Development.json con la cadena de conexión.");

        services.AddDbContext<ErpDbContext>(opt =>
            opt.UseSqlServer(connectionString), ServiceLifetime.Scoped);

        // IDbContextFactory: crea contextos aislados por operación para servicios
        // que requieren concurrencia (ej. PermisoService). Necesario en WinUI donde
        // Scoped actúa como Singleton y el contexto compartido causa concurrencia.
        services.AddDbContextFactory<ErpDbContext>(opt =>
            opt.UseSqlServer(connectionString), ServiceLifetime.Scoped);

        services.AddIdentityCore<ApplicationUser>()
                .AddRoles<ApplicationRole>()
                .AddEntityFrameworkStores<ErpDbContext>();

        // ── Application services ──────────────────────────────────────────────
        services.AddApplicationServices();

        // ── UI Services ───────────────────────────────────────────────────────
        services.AddSingleton<MainWindow>();
        services.AddSingleton<SessionService>();
        // Alias para que ErpDbContext reciba ISessionContext por DI
        services.AddSingleton<ISessionContext>(sp => sp.GetRequiredService<SessionService>());
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IWindowManager, WindowManager>(); // ADR-029: Centralized Window Management (incluye Window Detach Mode)
        services.AddSingleton<IWorkspaceService, WorkspaceService>();
        services.AddSingleton<IOperationalObservabilityService, OperationalObservabilityService>();
        services.AddSingleton<ICurrentContextTracker, CurrentContextTracker>();
        services.AddSingleton<RuntimeDiagnosticService>();
        services.AddTransient<DiagnosticPanelViewModel>();

        // ── ViewModels ────────────────────────────────────────────────────────
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<PosViewModel>();
        services.AddTransient<ProductosViewModel>();
        services.AddTransient<EntradasViewModel>();
        services.AddTransient<SalidasViewModel>();
        services.AddTransient<ExistenciasViewModel>();
        services.AddTransient<KardexViewModel>();
        services.AddTransient<ClientesViewModel>();
        services.AddTransient<CotizacionesViewModel>();
        services.AddTransient<PedidosViewModel>();
        services.AddTransient<VentasDocumentalesViewModel>();
        services.AddTransient<OrdenesTrabajoViewModel>();
        services.AddTransient<GastosViewModel>();
        services.AddTransient<IngresosViewModel>();
        services.AddTransient<CxCViewModel>();
        services.AddTransient<CxPViewModel>();
        services.AddTransient<EmpresaViewModel>();
        services.AddTransient<SucursalesConfigViewModel>();
        services.AddTransient<UsuariosViewModel>();
        services.AddTransient<RolesViewModel>();
        services.AddTransient<AuditoriaViewModel>();
        services.AddTransient<PerfilesViewModel>();
        services.AddTransient<PermisosViewModel>();
        services.AddTransient<ScopesViewModel>();

        // ── ViewModels — Configuración Global (nuevos) ────────────────────────
        services.AddTransient<ParametrosViewModel>();
        services.AddTransient<ImpuestosViewModel>();
        services.AddTransient<OtrosCargosViewModel>();
        services.AddTransient<UnidadesMedidaViewModel>();
        services.AddTransient<TiposProductoViewModel>();
        services.AddTransient<SeriesDocumentoViewModel>();

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
        services.AddTransient<EmpresaPage>();
        services.AddTransient<SucursalesConfigPage>();
        services.AddTransient<UsuariosPage>();
        services.AddTransient<RolesPage>();
        services.AddTransient<AuditoriaPage>();
        services.AddTransient<PerfilesPage>();
        services.AddTransient<PermisosPage>();
        services.AddTransient<ScopesPage>();
        services.AddTransient<ArquitecturaSegPage>();
        services.AddTransient<ParametrosPage>();
        services.AddTransient<ImpuestosPage>();
        services.AddTransient<OtrosCargosPage>();
        services.AddTransient<UnidadesMedidaPage>();
        services.AddTransient<TiposProductoPage>();
        services.AddTransient<WorkflowPage>();
        services.AddTransient<SeguridadGlobalPage>();
        services.AddTransient<SeriesDocumentoPage>();
        services.AddTransient<ClientesPage>();
        services.AddTransient<CotizacionesPage>();
        services.AddTransient<PedidosPage>();
        services.AddTransient<OrdenesTrabajoPage>();
        services.AddTransient<FinanzasPage>();
        services.AddTransient<GastosPage>();
        services.AddTransient<IngresosPage>();
        services.AddTransient<CxCPage>();
        services.AddTransient<CxPPage>();

        // ── Infraestructura técnica ───────────────────────────────────────────
        services.AddTransient<ISchemaAuditService, SchemaAuditService>();
        services.AddTransient<IDatabaseAuditService, DatabaseAuditService>();
        services.AddTransient<IWorkflowAuditService, WorkflowAuditService>();
        services.AddTransient<ICommercialIntegrityAuditService, CommercialIntegrityAuditService>();

        return services.BuildServiceProvider();
    }
}
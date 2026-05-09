using Microsoft.Extensions.DependencyInjection;
using Ybridio.Application.Services.Auth;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Application.Services.Caja;
using Ybridio.Application.Services.Venta;
using Ybridio.Application.Services.Finanzas;
using Ybridio.Application.Services.Inventario;
using Ybridio.Application.Services.Permisos;
using Ybridio.Application.Services.Producto;
using Ybridio.Application.Services.Empresa;
using Ybridio.Application.Services.Seguridad;
using Ybridio.Application.Services.Sucursal;
using Ybridio.Application.Services.Venta;

namespace Ybridio.Application.Extensions;

/// <summary>
/// Extensiones de IServiceCollection para registrar todos los servicios de la capa Application.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // ── Autenticación ─────────────────────────────────────────────────────
        services.AddScoped<IAuthService, AuthService>();

        // ── Negocio ───────────────────────────────────────────────────────────
        services.AddScoped<ICajaService, CajaService>();
        services.AddScoped<IVentaService, VentaService>();
        services.AddScoped<IInventarioService, InventarioService>();
        services.AddScoped<IProductoService, ProductoService>();
        services.AddScoped<ISucursalService, SucursalService>();
        services.AddScoped<IEmpresaService, EmpresaService>();
        services.AddScoped<IEntradaService, EntradaService>();
        services.AddScoped<ISalidaService, SalidaService>();
        services.AddScoped<IClienteService, ClienteService>();
        services.AddScoped<ICotizacionService, CotizacionService>();
        services.AddScoped<IPedidoService, PedidoService>();
        services.AddScoped<IOrdenTrabajoService, OrdenTrabajoService>();
        services.AddScoped<IFinanzasService, FinanzasService>();
        services.AddScoped<ICxCService, CxCService>();
        services.AddScoped<ICxPService, CxPService>();

        // ── Seguridad (usuarios, roles, perfiles) ─────────────────────────────
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<IRolService, RolService>();
        services.AddScoped<IPerfilService, PerfilService>();
        services.AddScoped<ISecurityAdminService, SecurityAdminService>();

        // ── Security Foundation Runtime ───────────────────────────────────────
        services.AddScoped<IPermisoService, PermisoService>();
        services.AddScoped<ISecurityScopeResolver, SecurityScopeResolver>();
        services.AddScoped<ISecurityContextService, SecurityContextService>();
        services.AddScoped<IErpAuthorizationService, ErpAuthorizationService>();

        // ── Caché de permisos: MemoryPermissionCache (TTL 10 min) ─────────────
        // Singleton para que el caché persista entre scopes; thread-safe con ConcurrentDictionary.
        services.AddSingleton<IPermissionCache, MemoryPermissionCache>();

        return services;
    }
}

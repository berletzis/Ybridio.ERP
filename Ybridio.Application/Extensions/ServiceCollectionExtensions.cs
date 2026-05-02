// ── ServiceCollectionExtensions.cs — REEMPLAZAR COMPLETO ─────────────────────
using Microsoft.Extensions.DependencyInjection;
using Ybridio.Application.Services.Auth;
using Ybridio.Application.Services.Caja;
using Ybridio.Application.Services.Inventario;
using Ybridio.Application.Services.Permisos;
using Ybridio.Application.Services.Producto;
using Ybridio.Application.Services.Venta;

namespace Ybridio.Application.Extensions;

/// <summary>
/// Extensiones de IServiceCollection para registrar todos los servicios de la capa Application.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICajaService, CajaService>();
        services.AddScoped<IVentaService, VentaService>();
        services.AddScoped<IInventarioService, InventarioService>();
        services.AddScoped<IPermisoService, PermisoService>();
        services.AddScoped<IProductoService, ProductoService>();  // ← NUEVO

        // Caché de permisos: NullCache por defecto
        services.AddSingleton<IPermissionCache, NullPermissionCache>();

        return services;
    }
}
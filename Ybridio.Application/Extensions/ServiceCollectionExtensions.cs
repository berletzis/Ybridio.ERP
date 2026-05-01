using Microsoft.Extensions.DependencyInjection;
using Ybridio.Application.Services.Auth;
using Ybridio.Application.Services.Caja;
using Ybridio.Application.Services.Inventario;
using Ybridio.Application.Services.Permisos;
using Ybridio.Application.Services.Venta;

namespace Ybridio.Application.Extensions;

/// <summary>
/// Extensiones de IServiceCollection para registrar todos los servicios de la capa Application.
/// Llamar desde el punto de arranque de la aplicación (App.xaml.cs o similar).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra los servicios de negocio de Application como Scoped.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICajaService, CajaService>();
        services.AddScoped<IVentaService, VentaService>();
        services.AddScoped<IInventarioService, InventarioService>();
        services.AddScoped<IPermisoService, PermisoService>();

        // Caché de permisos: implementación nula por defecto.
        // Sustituir por una implementación real (MemoryCache, Redis, etc.) cuando sea necesario.
        services.AddSingleton<IPermissionCache, NullPermissionCache>();

        return services;
    }
}

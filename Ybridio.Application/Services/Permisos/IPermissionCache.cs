namespace Ybridio.Application.Services.Permisos;

/// <summary>
/// Abstracción de caché de permisos. Permite sustituir la consulta a base de datos
/// por una implementación en memoria o distribuida sin modificar <see cref="PermisoService"/>.
/// </summary>
/// <remarks>
/// Esta interfaz está preparada para uso futuro. La implementación por defecto
/// (<c>NullPermissionCache</c>) siempre devuelve vacío y fuerza la consulta a la BD.
/// </remarks>
public interface IPermissionCache
{
    /// <summary>
    /// Retorna los permisos del usuario desde caché, o vacío si no están cacheados.
    /// </summary>
    Task<IReadOnlyList<string>> GetPermisosAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Almacena los permisos del usuario en caché.
    /// </summary>
    Task SetPermisosAsync(Guid usuarioId, IReadOnlyList<string> permisos, CancellationToken ct = default);

    /// <summary>
    /// Invalida la caché del usuario (p.ej. tras cambio de roles o permisos).
    /// </summary>
    Task InvalidateAsync(Guid usuarioId, CancellationToken ct = default);
}

/// <summary>
/// Implementación nula que desactiva el caché. Toda consulta irá a la base de datos.
/// Sustituir por una implementación real (MemoryCache, Redis, etc.) cuando sea necesario.
/// </summary>
internal sealed class NullPermissionCache : IPermissionCache
{
    public Task<IReadOnlyList<string>> GetPermisosAsync(Guid usuarioId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task SetPermisosAsync(Guid usuarioId, IReadOnlyList<string> permisos, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task InvalidateAsync(Guid usuarioId, CancellationToken ct = default) =>
        Task.CompletedTask;
}

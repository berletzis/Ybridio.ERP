using System.Collections.Concurrent;

namespace Ybridio.Application.Services.Permisos;

/// <summary>
/// Implementación en memoria del caché de permisos.
/// Almacena los permisos efectivos de cada usuario con TTL configurable.
/// Singleton — comparte estado entre todos los scopes del contenedor DI.
/// Thread-safe gracias a ConcurrentDictionary.
/// </summary>
/// <remarks>
/// Sustituto funcional de <c>NullPermissionCache</c> para reducir queries a BD.
/// Para entornos distribuidos (múltiples procesos), reemplazar por Redis u otro
/// caché compartido implementando <see cref="IPermissionCache"/>.
/// </remarks>
public sealed class MemoryPermissionCache : IPermissionCache
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<Guid, CacheEntry> _store = new();

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetPermisosAsync(
        Guid usuarioId, CancellationToken ct = default)
    {
        if (_store.TryGetValue(usuarioId, out var entry) && !entry.IsExpired)
            return Task.FromResult(entry.Permisos);

        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    /// <inheritdoc/>
    public Task SetPermisosAsync(
        Guid usuarioId, IReadOnlyList<string> permisos, CancellationToken ct = default)
    {
        _store[usuarioId] = new CacheEntry(permisos, DateTime.UtcNow.Add(DefaultTtl));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task InvalidateAsync(Guid usuarioId, CancellationToken ct = default)
    {
        _store.TryRemove(usuarioId, out _);
        return Task.CompletedTask;
    }

    /// <summary>Invalida todo el caché (útil en cambios masivos de roles/permisos).</summary>
    public void InvalidateAll() => _store.Clear();

    private sealed record CacheEntry(IReadOnlyList<string> Permisos, DateTime ExpiresAt)
    {
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}

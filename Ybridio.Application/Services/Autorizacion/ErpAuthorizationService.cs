using Ybridio.Application.DTOs.Seguridad;
using Ybridio.Application.Services.Permisos;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Autorizacion;

/// <summary>
/// Implementación del motor de autorización runtime.
/// Delega la evaluación de permisos en <see cref="IPermisoService"/> y la resolución de scopes
/// en <see cref="ISecurityScopeResolver"/>. Obtiene el usuario actual desde <see cref="ISessionContext"/>.
/// </summary>
public sealed class ErpAuthorizationService : IErpAuthorizationService
{
    private readonly ISessionContext        _session;
    private readonly IPermisoService        _permisos;
    private readonly ISecurityContextService _securityCtx;
    private readonly ISecurityScopeResolver  _scopeResolver;

    public ErpAuthorizationService(
        ISessionContext         session,
        IPermisoService         permisos,
        ISecurityContextService securityCtx,
        ISecurityScopeResolver  scopeResolver)
    {
        _session       = session;
        _permisos      = permisos;
        _securityCtx   = securityCtx;
        _scopeResolver = scopeResolver;
    }

    /// <inheritdoc/>
    public async Task<bool> PuedeAsync(string clave, CancellationToken ct = default)
    {
        if (_session.UsuarioId is not { } uid)
            return false;

        return await _permisos.TienePermisoAsync(uid, clave, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> PuedeAsync(Guid usuarioId, string clave, CancellationToken ct = default) =>
        await _permisos.TienePermisoAsync(usuarioId, clave, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlySet<string>?> ObtenerPermisosEfectivosAsync(CancellationToken ct = default)
    {
        if (_session.UsuarioId is not { } uid)
            return null;

        return await _permisos.ObtenerPermisosEfectivosAsync(uid, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlySet<string>> ObtenerPermisosEfectivosAsync(
        Guid usuarioId, CancellationToken ct = default) =>
        await _permisos.ObtenerPermisosEfectivosAsync(usuarioId, ct);

    /// <inheritdoc/>
    public async Task<SecurityContextDto?> ObtenerContextoSeguridad(CancellationToken ct = default)
    {
        if (_session.UsuarioId is not { } uid)
            return null;

        return await _securityCtx.ObtenerContextoAsync(uid, ct);
    }
}

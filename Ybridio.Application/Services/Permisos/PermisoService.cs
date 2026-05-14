using Microsoft.EntityFrameworkCore;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Permisos;

/// <summary>
/// Evaluación de permisos con tres niveles de resolución:
/// 1. Override explícito de usuario (UsuarioPermiso.Permitido = true/false)
/// 2. Permisos de perfiles asignados al usuario (UsuarioPerfil → PerfilPermiso)
/// 3. Herencia desde roles del usuario (RolPermiso)
/// Un denegado explícito (override = false) siempre tiene prioridad absoluta.
/// </summary>
/// <remarks>
/// Usa <see cref="IDbContextFactory{TContext}"/> para crear un contexto aislado por evaluación.
/// TODOS los queries — incluidos los de roles — van por el contexto factory, nunca por el contexto
/// scoped compartido. Esto evita "A second operation was started on this context instance" cuando
/// múltiples evaluaciones de permisos ocurren concurrentemente (navegación rápida, Document Surfaces,
/// Workspace Tabs, Runtime Diagnostic Panel). ADR-026.
///
/// PROHIBIDO: inyectar UserManager aquí — su UserStore usa el DbContext scoped compartido y
/// causaría la misma excepción de concurrencia que este diseño busca evitar.
/// </remarks>
public sealed class PermisoService : IPermisoService
{
    private readonly IDbContextFactory<ErpDbContext> _contextFactory;
    private readonly IPermissionCache                _cache;

    public PermisoService(
        IDbContextFactory<ErpDbContext> contextFactory,
        IPermissionCache                cache)
    {
        _contextFactory = contextFactory;
        _cache          = cache;
    }

    /// <inheritdoc/>
    public async Task<bool> TienePermisoAsync(
        Guid usuarioId, string clave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clave))
            return false;

        // Contexto aislado — independiente del DbContext scoped compartido
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        // ── NIVEL 1: override explícito del usuario ───────────────────────────
        var overrideUsuario = await ctx.UsuariosPermisos
            .AsNoTracking()
            .Where(up => up.UsuarioId == usuarioId
                      && up.Permiso.Clave == clave
                      && !up.Permiso.Borrado)
            .Select(up => up.Permitido)
            .FirstOrDefaultAsync(ct);

        if (overrideUsuario.HasValue)
            return overrideUsuario.Value;

        // ── NIVEL 2: perfiles asignados al usuario ────────────────────────────
        var tienePermisoPerfil = await ctx.UsuariosPerfiles
            .AsNoTracking()
            .Where(up => up.UsuarioId == usuarioId && up.Perfil.Activo && !up.Perfil.Borrado)
            .SelectMany(up => up.Perfil.PerfilPermisos)
            .AnyAsync(pp => pp.Permiso.Clave == clave && !pp.Permiso.Borrado, ct);

        if (tienePermisoPerfil)
            return true;

        // ── NIVEL 3: herencia desde roles del usuario ─────────────────────────
        // Query directo en ctx (NO UserManager — su store usa el DbContext scoped compartido)
        var rolesDelUsuario = await ctx.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == usuarioId)
            .Join(ctx.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
            .ToListAsync(ct);

        if (rolesDelUsuario.Count == 0)
            return false;

        return await ctx.RolesPermisos
            .AsNoTracking()
            .Where(rp => rp.Permiso.Clave == clave
                      && !rp.Permiso.Borrado
                      && rp.Permitido)
            .Join(
                ctx.Roles.Where(r => rolesDelUsuario.Contains(r.Name!)),
                rp => rp.RolId,
                r  => r.Id,
                (rp, _) => rp.Id)
            .AnyAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlySet<string>> ObtenerPermisosEfectivosAsync(
        Guid usuarioId, CancellationToken ct = default)
    {
        var cached = await _cache.GetPermisosAsync(usuarioId, ct);
        if (cached.Count > 0)
            return new HashSet<string>(cached, StringComparer.OrdinalIgnoreCase);

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        // ── NIVEL 1: overrides de usuario ─────────────────────────────────────
        var overrides = await ctx.UsuariosPermisos
            .AsNoTracking()
            .Where(up => up.UsuarioId == usuarioId && !up.Permiso.Borrado)
            .Select(up => new { up.Permiso.Clave, up.Permitido })
            .ToListAsync(ct);

        var resultado = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var denegados = overrides
            .Where(o => o.Permitido == false)
            .Select(o => o.Clave)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var o in overrides.Where(o => o.Permitido == true))
            resultado.Add(o.Clave);

        // ── NIVEL 2: perfiles asignados al usuario ────────────────────────────
        var permisosDePerfil = await ctx.UsuariosPerfiles
            .AsNoTracking()
            .Where(up => up.UsuarioId == usuarioId && up.Perfil.Activo && !up.Perfil.Borrado)
            .SelectMany(up => up.Perfil.PerfilPermisos)
            .Where(pp => !pp.Permiso.Borrado)
            .Select(pp => pp.Permiso.Clave)
            .Distinct()
            .ToListAsync(ct);

        foreach (var clave in permisosDePerfil)
            if (!denegados.Contains(clave))
                resultado.Add(clave);

        // ── NIVEL 3: herencia de roles ────────────────────────────────────────
        // Query directo en ctx (NO UserManager — evita colisión con DbContext scoped)
        var rolesDelUsuario = await ctx.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == usuarioId)
            .Join(ctx.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
            .ToListAsync(ct);

        if (rolesDelUsuario.Count > 0)
        {
            var permisosDeRol = await ctx.RolesPermisos
                .AsNoTracking()
                .Where(rp => rp.Permitido && !rp.Permiso.Borrado)
                .Join(
                    ctx.Roles.Where(r => rolesDelUsuario.Contains(r.Name!)),
                    rp => rp.RolId,
                    r  => r.Id,
                    (rp, _) => rp.Permiso.Clave)
                .Distinct()
                .ToListAsync(ct);

            foreach (var clave in permisosDeRol)
                if (!denegados.Contains(clave))
                    resultado.Add(clave);
        }

        await _cache.SetPermisosAsync(usuarioId, resultado.ToList(), ct);
        return resultado;
    }
}

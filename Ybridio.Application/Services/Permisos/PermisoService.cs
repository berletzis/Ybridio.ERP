using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ybridio.Infrastructure.Persistence;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Application.Services.Permisos;

/// <summary>
/// Evaluación de permisos con tres niveles de resolución:
/// 1. Override explícito de usuario (UsuarioPermiso.Permitido = true/false)
/// 2. Permisos de perfiles asignados al usuario (UsuarioPerfil → PerfilPermiso)
/// 3. Herencia desde roles del usuario (RolPermiso)
/// Un denegado explícito (override = false) siempre tiene prioridad absoluta.
/// </summary>
/// <remarks>
/// Usa IDbContextFactory para crear un contexto aislado por evaluación.
/// En WinUI (sin HTTP scope), Scoped actúa como Singleton en el contenedor raíz;
/// todos los servicios comparten la misma instancia de ErpDbContext. El factory
/// crea contextos de vida corta por llamada, evitando la excepción
/// "A second operation was started on this context instance" cuando múltiples
/// servicios disparan queries concurrentes al abrir un módulo.
/// </remarks>
public sealed class PermisoService : IPermisoService
{
    private readonly IDbContextFactory<ErpDbContext> _contextFactory;
    private readonly UserManager<ApplicationUser>    _userManager;
    private readonly IPermissionCache                _cache;

    public PermisoService(
        IDbContextFactory<ErpDbContext> contextFactory,
        UserManager<ApplicationUser>    userManager,
        IPermissionCache                cache)
    {
        _contextFactory = contextFactory;
        _userManager    = userManager;
        _cache          = cache;
    }

    /// <inheritdoc/>
    public async Task<bool> TienePermisoAsync(
        Guid usuarioId, string clave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clave))
            return false;

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        // 1. Override explícito del usuario (bool? — null = hereda)
        var overrideUsuario = await ctx.UsuariosPermisos
            .AsNoTracking()
            .Where(up => up.UsuarioId == usuarioId
                      && up.Permiso.Clave == clave
                      && !up.Permiso.Borrado)
            .Select(up => up.Permitido)
            .FirstOrDefaultAsync(ct);

        if (overrideUsuario.HasValue)
            return overrideUsuario.Value;   // true = permitido, false = denegado explícito

        // 2. Permisos de perfiles asignados al usuario
        var tienePermisoPerfil = await ctx.UsuariosPerfiles
            .AsNoTracking()
            .Where(up => up.UsuarioId == usuarioId && up.Perfil.Activo && !up.Perfil.Borrado)
            .SelectMany(up => up.Perfil.PerfilPermisos)
            .AnyAsync(pp => pp.Permiso.Clave == clave && !pp.Permiso.Borrado, ct);

        if (tienePermisoPerfil)
            return true;

        // 3. Herencia desde roles del usuario
        var usuario = await _userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null) return false;

        var rolesUsuario = await _userManager.GetRolesAsync(usuario);
        if (rolesUsuario.Count == 0) return false;

        var permitidoPorRol = await ctx.RolesPermisos
            .AsNoTracking()
            .Where(rp => rp.Permiso.Clave == clave
                      && !rp.Permiso.Borrado
                      && rp.Permitido)
            .Join(
                ctx.Roles
                    .Where(r => rolesUsuario.Contains(r.Name!)),
                rp => rp.RolId,
                r => r.Id,
                (rp, _) => rp.Id)
            .AnyAsync(ct);

        return permitidoPorRol;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlySet<string>> ObtenerPermisosEfectivosAsync(
        Guid usuarioId, CancellationToken ct = default)
    {
        // Intentar desde caché
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
        var usuario = await _userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is not null)
        {
            var rolesUsuario = await _userManager.GetRolesAsync(usuario);

            if (rolesUsuario.Count > 0)
            {
                // Usar ctx.Roles (ApplicationRole) NO ctx.Set<IdentityRole<Guid>>()
                // ErpDbContext registra ApplicationRole, no el tipo genérico base de Identity.
                var permisosDeRol = await ctx.RolesPermisos
                    .AsNoTracking()
                    .Where(rp => rp.Permitido && !rp.Permiso.Borrado)
                    .Join(
                        ctx.Roles
                            .Where(r => rolesUsuario.Contains(r.Name!)),
                        rp => rp.RolId,
                        r => r.Id,
                        (rp, _) => rp.Permiso.Clave)
                    .Distinct()
                    .ToListAsync(ct);

                foreach (var clave in permisosDeRol)
                    if (!denegados.Contains(clave))
                        resultado.Add(clave);
            }
        }

        await _cache.SetPermisosAsync(usuarioId, resultado.ToList(), ct);
        return resultado;
    }
}

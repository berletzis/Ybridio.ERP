using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ybridio.Infrastructure.Persistence;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Application.Services.Permisos;

/// <summary>
/// Evaluación de permisos con lógica de override por usuario y herencia de rol.
/// </summary>
public sealed class PermisoService : IPermisoService
{
    private readonly ErpDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPermissionCache _cache;

    public PermisoService(
        ErpDbContext context,
        UserManager<ApplicationUser> userManager,
        IPermissionCache cache)
    {
        _context = context;
        _userManager = userManager;
        _cache = cache;
    }

    /// <inheritdoc/>
    public async Task<bool> TienePermisoAsync(
        Guid usuarioId, string clave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clave))
            return false;

        // 1. Override explícito del usuario
        var overrideUsuario = await _context.UsuariosPermisos
            .AsNoTracking()
            .Where(up => up.UsuarioId == usuarioId
                      && up.Permiso.Clave == clave
                      && !up.Permiso.Borrado)
            .Select(up => up.Permitido)
            .FirstOrDefaultAsync(ct);

        // overrideUsuario es bool? — si tiene valor, aplica directamente
        if (overrideUsuario.HasValue)
            return overrideUsuario.Value;

        // 2. Herencia desde roles del usuario
        var rolesUsuario = await _userManager.GetRolesAsync(
            (await _userManager.FindByIdAsync(usuarioId.ToString()))!);

        if (rolesUsuario.Count == 0)
            return false;

        var permitidoPorRol = await _context.RolesPermisos
            .AsNoTracking()
            .Where(rp => rolesUsuario.Contains(rp.Permiso.Modulo.Nombre == string.Empty
                             ? rp.Permiso.Clave  // fallback; ver nota abajo
                             : rp.Permiso.Clave)
                      && rp.Permiso.Clave == clave
                      && !rp.Permiso.Borrado
                      && rp.Permitido)
            .AnyAsync(ct);

        return permitidoPorRol;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlySet<string>> ObtenerPermisosEfectivosAsync(
        Guid usuarioId, CancellationToken ct = default)
    {
        // Intentar desde caché (NullPermissionCache siempre devuelve vacío)
        var cached = await _cache.GetPermisosAsync(usuarioId, ct);
        if (cached.Count > 0)
            return new HashSet<string>(cached, StringComparer.OrdinalIgnoreCase);

        // Cargar overrides del usuario: clave → bool?
        var overrides = await _context.UsuariosPermisos
            .AsNoTracking()
            .Where(up => up.UsuarioId == usuarioId && !up.Permiso.Borrado)
            .Select(up => new { up.Permiso.Clave, up.Permitido })
            .ToListAsync(ct);

        var resultado = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Overrides explícitamente denegados
        var denegados = overrides
            .Where(o => o.Permitido == false)
            .Select(o => o.Clave)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Overrides explícitamente permitidos
        foreach (var o in overrides.Where(o => o.Permitido == true))
            resultado.Add(o.Clave);

        // Herencia de roles (excluyendo denegados y ya incluidos)
        var usuario = await _userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is not null)
        {
            var rolesUsuario = await _userManager.GetRolesAsync(usuario);

            if (rolesUsuario.Count > 0)
            {
                var permisosDeRol = await _context.RolesPermisos
                    .AsNoTracking()
                    .Where(rp => rp.Permitido && !rp.Permiso.Borrado)
                    .Join(
                        _context.Set<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()
                            .Where(r => rolesUsuario.Contains(r.Name!)),
                        rp => rp.RolId,
                        r => r.Id,
                        (rp, _) => rp.Permiso.Clave)
                    .Distinct()
                    .ToListAsync(ct);

                foreach (var clave in permisosDeRol)
                {
                    if (!denegados.Contains(clave))
                        resultado.Add(clave);
                }
            }
        }

        // Almacenar en caché para futuras consultas (no-op con NullPermissionCache)
        await _cache.SetPermisosAsync(usuarioId, resultado.ToList(), ct);

        return resultado;
    }
}

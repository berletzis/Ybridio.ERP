using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.Domain.Seguridad;
using Ybridio.Infrastructure.Persistence;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Application.Services.Seguridad;

/// <summary>
/// Implementación del servicio de administración de seguridad.
/// Provee queries enriquecidas y operaciones de asignación para la UI administrativa.
/// Utiliza UserManager e Identity directamente donde corresponde; accede a la BD vía ErpDbContext
/// para entidades propias del dominio de seguridad.
/// </summary>
public sealed class SecurityAdminService : ISecurityAdminService
{
    private readonly ErpDbContext                 _context;
    private readonly UserManager<ApplicationUser> _users;

    public SecurityAdminService(ErpDbContext context, UserManager<ApplicationUser> users)
    {
        _context = context;
        _users   = users;
    }

    // ── Usuarios ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UsuarioResumenDto>> ListarUsuariosConDetalleAsync(
        int empresaId, CancellationToken ct = default)
    {
        var usuarios = await _context.Users
            .AsNoTracking()
            .Where(u => u.EmpresaId == empresaId && !u.Borrado)
            .OrderBy(u => u.Nombre)
            .ToListAsync(ct);

        var result = new List<UsuarioResumenDto>(usuarios.Count);
        foreach (var u in usuarios)
        {
            var roles    = await _users.GetRolesAsync(u);
            var perfiles = await _context.UsuariosPerfiles
                .AsNoTracking()
                .Where(up => up.UsuarioId == u.Id)
                .Select(up => up.Perfil.Nombre)
                .ToListAsync(ct);

            result.Add(new UsuarioResumenDto(
                u.Id,
                u.EmpresaId,
                u.Nombre,
                u.UserName ?? string.Empty,
                u.Email,
                u.Activo,
                string.Join(", ", roles.OrderBy(r => r)),
                string.Join(", ", perfiles.OrderBy(p => p))));
        }
        return result;
    }

    // ── Roles ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RolAdminDto>> ListarRolesConDetalleAsync(CancellationToken ct = default)
    {
        var roles = await _context.Roles
            .AsNoTracking()
            .Where(r => !r.Borrado)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        var result = new List<RolAdminDto>(roles.Count);
        foreach (var r in roles)
        {
            var cantPermisos = await _context.RolesPermisos
                .CountAsync(rp => rp.RolId == r.Id, ct);

            var cantUsuarios = await _context.Set<IdentityUserRole<Guid>>()
                .CountAsync(ur => ur.RoleId == r.Id, ct);

            result.Add(new RolAdminDto(r.Id, r.Name ?? string.Empty, cantPermisos, cantUsuarios));
        }
        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<int>> ObtenerPermisosDeRolAsync(Guid rolId, CancellationToken ct = default)
        => await _context.RolesPermisos
            .AsNoTracking()
            .Where(rp => rp.RolId == rolId && rp.Permitido)
            .Select(rp => rp.PermisoId)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<ServiceResult> AsignarPermisosARolAsync(
        Guid rolId, IReadOnlyList<int> permisoIds, CancellationToken ct = default)
    {
        var existentes = await _context.RolesPermisos
            .Where(rp => rp.RolId == rolId)
            .ToListAsync(ct);

        _context.RolesPermisos.RemoveRange(existentes);

        foreach (var pid in permisoIds)
        {
            _context.RolesPermisos.Add(new RolPermiso
            {
                RolId     = rolId,
                PermisoId = pid,
                Permitido = true,
            });
        }

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // ── Permisos ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PermisoAdminDto>> ListarPermisosAsync(CancellationToken ct = default)
    {
        var permisos = await _context.Permisos
            .AsNoTracking()
            .Include(p => p.Modulo)
            .Where(p => !p.Borrado)
            .OrderBy(p => p.Modulo.Orden)
            .ThenBy(p => p.Nombre)
            .ToListAsync(ct);

        return permisos
            .Select(p => new PermisoAdminDto(p.Id, p.Clave, p.Modulo.Nombre, p.Modulo.Clave, p.Nombre))
            .ToList();
    }

    // ── Scopes ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ScopeUsuarioDto>> ListarScopesUsuariosAsync(
        int empresaId, CancellationToken ct = default)
    {
        var usuarios = await _context.Users
            .AsNoTracking()
            .Where(u => u.EmpresaId == empresaId && !u.Borrado)
            .OrderBy(u => u.Nombre)
            .ToListAsync(ct);

        var result = new List<ScopeUsuarioDto>(usuarios.Count);
        foreach (var u in usuarios)
        {
            var roles         = await _users.GetRolesAsync(u);
            var esSuperAdmin  = roles.Contains("SuperAdmin");

            var sucursales = await _context.UsuariosSucursales
                .AsNoTracking()
                .Where(us => us.UsuarioId == u.Id && us.SucursalId.HasValue)
                .Join(_context.Sucursales,
                      us => us.SucursalId!.Value,
                      s  => s.Id,
                      (us, s) => s.Nombre)
                .OrderBy(n => n)
                .ToListAsync(ct);

            var almacenes = await _context.UsuariosAlmacenes
                .AsNoTracking()
                .Where(ua => ua.UsuarioId == u.Id)
                .Join(_context.Almacenes,
                      ua => ua.AlmacenId,
                      a  => a.Id,
                      (ua, a) => a.Nombre)
                .OrderBy(n => n)
                .ToListAsync(ct);

            result.Add(new ScopeUsuarioDto(
                u.Id,
                u.Nombre,
                esSuperAdmin,
                sucursales.Count,
                almacenes.Count,
                esSuperAdmin ? "SuperAdmin (todas)" : (sucursales.Count > 0 ? string.Join(", ", sucursales) : "Sin restricción"),
                esSuperAdmin ? "SuperAdmin (todos)" : (almacenes.Count  > 0 ? string.Join(", ", almacenes)  : "Sin restricción")));
        }
        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SucursalScopeItem>> ListarSucursalesDisponiblesAsync(
        int empresaId, CancellationToken ct = default)
    {
        var lista = await _context.Sucursales
            .AsNoTracking()
            .Where(s => s.EmpresaId == empresaId)
            .OrderBy(s => s.Nombre)
            .Select(s => new SucursalScopeItem(s.Id, s.Nombre))
            .ToListAsync(ct);
        return lista;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AlmacenScopeItem>> ListarAlmacenesDisponiblesAsync(
        int empresaId, CancellationToken ct = default)
    {
        var lista = await _context.Almacenes
            .AsNoTracking()
            .Where(a => a.EmpresaId == empresaId)
            .OrderBy(a => a.Nombre)
            .Select(a => new AlmacenScopeItem(a.Id, a.Nombre))
            .ToListAsync(ct);
        return lista;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<int>> ObtenerSucursalesDeUsuarioAsync(
        Guid usuarioId, CancellationToken ct = default)
        => await _context.UsuariosSucursales
            .AsNoTracking()
            .Where(us => us.UsuarioId == usuarioId && us.SucursalId.HasValue)
            .Select(us => us.SucursalId!.Value)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<ServiceResult> AsignarSucursalesAUsuarioAsync(
        Guid usuarioId, IReadOnlyList<int> sucursalIds, CancellationToken ct = default)
    {
        var existentes = await _context.UsuariosSucursales
            .Where(us => us.UsuarioId == usuarioId)
            .ToListAsync(ct);
        _context.UsuariosSucursales.RemoveRange(existentes);

        foreach (var sid in sucursalIds)
            _context.UsuariosSucursales.Add(new Domain.Seguridad.UsuarioSucursal { UsuarioId = usuarioId, SucursalId = sid });

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<int>> ObtenerAlmacenesDeUsuarioAsync(
        Guid usuarioId, CancellationToken ct = default)
        => await _context.UsuariosAlmacenes
            .AsNoTracking()
            .Where(ua => ua.UsuarioId == usuarioId)
            .Select(ua => ua.AlmacenId)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<ServiceResult> AsignarAlmacenesAUsuarioAsync(
        Guid usuarioId, IReadOnlyList<int> almacenIds, CancellationToken ct = default)
    {
        var existentes = await _context.UsuariosAlmacenes
            .Where(ua => ua.UsuarioId == usuarioId)
            .ToListAsync(ct);
        _context.UsuariosAlmacenes.RemoveRange(existentes);

        foreach (var aid in almacenIds)
            _context.UsuariosAlmacenes.Add(new Domain.Seguridad.UsuarioAlmacen { UsuarioId = usuarioId, AlmacenId = aid });

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<int>> ObtenerPermisosDePerfilAsync(int perfilId, CancellationToken ct = default)
        => await _context.PerfilPermisos
            .AsNoTracking()
            .Where(pp => pp.PerfilId == perfilId)
            .Select(pp => pp.PermisoId)
            .ToListAsync(ct);

    // ── Perfiles de usuario ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<int>> ObtenerPerfilesDeUsuarioAsync(
        Guid usuarioId, CancellationToken ct = default)
        => await _context.UsuariosPerfiles
            .AsNoTracking()
            .Where(up => up.UsuarioId == usuarioId)
            .Select(up => up.PerfilId)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<ServiceResult> AsignarPerfilesAUsuarioAsync(
        Guid usuarioId, IReadOnlyList<int> perfilIds, CancellationToken ct = default)
    {
        var existentes = await _context.UsuariosPerfiles
            .Where(up => up.UsuarioId == usuarioId)
            .ToListAsync(ct);
        _context.UsuariosPerfiles.RemoveRange(existentes);

        foreach (var pid in perfilIds)
            _context.UsuariosPerfiles.Add(new Domain.Seguridad.UsuarioPerfil { UsuarioId = usuarioId, PerfilId = pid });

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }
}

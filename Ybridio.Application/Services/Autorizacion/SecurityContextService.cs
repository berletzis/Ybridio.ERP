using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.Application.Services.Permisos;
using Ybridio.Infrastructure.Persistence;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Application.Services.Autorizacion;

/// <summary>
/// Construye el <see cref="SecurityContextDto"/> completo para un usuario,
/// agregando roles, perfiles, permisos efectivos y scopes de sucursal/almacén.
/// </summary>
public sealed class SecurityContextService : ISecurityContextService
{
    private readonly ISessionContext               _session;
    private readonly IPermisoService               _permisos;
    private readonly ISecurityScopeResolver        _scopeResolver;
    private readonly UserManager<ApplicationUser>  _userManager;
    private readonly ErpDbContext                  _context;

    public SecurityContextService(
        ISessionContext              session,
        IPermisoService              permisos,
        ISecurityScopeResolver       scopeResolver,
        UserManager<ApplicationUser> userManager,
        ErpDbContext                 context)
    {
        _session       = session;
        _permisos      = permisos;
        _scopeResolver = scopeResolver;
        _userManager   = userManager;
        _context       = context;
    }

    /// <inheritdoc/>
    public async Task<SecurityContextDto?> ObtenerContextoAsync(CancellationToken ct = default)
    {
        if (_session.UsuarioId is not { } uid)
            return null;

        return await ObtenerContextoAsync(uid, ct);
    }

    /// <inheritdoc/>
    public async Task<SecurityContextDto?> ObtenerContextoAsync(
        Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await _userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null || !usuario.Activo || usuario.Borrado)
            return null;

        // Roles
        var roles = await _userManager.GetRolesAsync(usuario);

        // Perfiles asignados
        var perfiles = await _context.UsuariosPerfiles
            .AsNoTracking()
            .Where(up => up.UsuarioId == usuarioId && up.Perfil.Activo && !up.Perfil.Borrado)
            .Select(up => up.Perfil.Nombre)
            .ToListAsync(ct);

        // Permisos efectivos
        var permisosEfectivos = await _permisos.ObtenerPermisosEfectivosAsync(usuarioId, ct);

        // Scopes
        var sucursales = await _scopeResolver.ObtenerSucursalesPermitidasAsync(usuarioId, ct);
        var almacenes  = await _scopeResolver.ObtenerAlmacentesPermitidosAsync(usuarioId, ct);
        var esSuperAdmin = await _scopeResolver.EsSuperAdminAsync(usuarioId, ct);

        return new SecurityContextDto(
            UsuarioId:            usuarioId,
            UsuarioNombre:        usuario.Nombre,
            EmpresaId:            usuario.EmpresaId,
            SucursalId:           _session.SucursalId,
            Roles:                roles.ToList(),
            Perfiles:             perfiles,
            PermisosEfectivos:    permisosEfectivos,
            SucursalesPermitidas: sucursales,
            AlmacentesPermitidos: almacenes,
            EsSuperAdmin:         esSuperAdmin,
            GeneradoEn:           DateTime.UtcNow);
    }
}

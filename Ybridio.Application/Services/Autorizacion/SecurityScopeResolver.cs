using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ybridio.Infrastructure.Persistence;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Application.Services.Autorizacion;

/// <summary>
/// Resuelve scopes de seguridad consultando <c>seguridad.UsuarioSucursal</c>
/// y <c>seguridad.UsuarioAlmacen</c>.
/// Los SuperAdmins (rol "SuperAdmin") no tienen restricciones de scope.
/// </summary>
public sealed class SecurityScopeResolver : ISecurityScopeResolver
{
    private const string RolSuperAdmin = "SuperAdmin";

    private readonly ErpDbContext                 _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public SecurityScopeResolver(
        ErpDbContext                 context,
        UserManager<ApplicationUser> userManager)
    {
        _context     = context;
        _userManager = userManager;
    }

    /// <inheritdoc/>
    public async Task<bool> EsSuperAdminAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await _userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null) return false;
        return await _userManager.IsInRoleAsync(usuario, RolSuperAdmin);
    }

    /// <inheritdoc/>
    public async Task<bool> TieneAccesoSucursalAsync(
        Guid usuarioId, int sucursalId, CancellationToken ct = default)
    {
        if (await EsSuperAdminAsync(usuarioId, ct))
            return true;

        return await _context.UsuariosSucursales
            .AsNoTracking()
            .AnyAsync(us => us.UsuarioId == usuarioId && us.SucursalId == sucursalId, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> TieneAccesoAlmacenAsync(
        Guid usuarioId, int almacenId, CancellationToken ct = default)
    {
        if (await EsSuperAdminAsync(usuarioId, ct))
            return true;

        // Tiene almacén explícito asignado → verificar directo
        var tieneAlmacenExplicito = await _context.UsuariosAlmacenes
            .AsNoTracking()
            .AnyAsync(ua => ua.UsuarioId == usuarioId, ct);

        if (tieneAlmacenExplicito)
            return await _context.UsuariosAlmacenes
                .AsNoTracking()
                .AnyAsync(ua => ua.UsuarioId == usuarioId && ua.AlmacenId == almacenId, ct);

        // Sin almacenes explícitos: verificar que la sucursal del almacén esté permitida
        var sucursalIdAlmacen = await _context.Almacenes
            .AsNoTracking()
            .Where(a => a.Id == almacenId)
            .Select(a => (int?)a.SucursalId)
            .FirstOrDefaultAsync(ct);

        if (sucursalIdAlmacen is null)
            return false;

        return await TieneAccesoSucursalAsync(usuarioId, sucursalIdAlmacen.Value, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<int>> ObtenerSucursalesPermitidasAsync(
        Guid usuarioId, CancellationToken ct = default)
    {
        if (await EsSuperAdminAsync(usuarioId, ct))
            return [];  // Vacío = sin restricción (SuperAdmin ve todo)

        return await _context.UsuariosSucursales
            .AsNoTracking()
            .Where(us => us.UsuarioId == usuarioId && us.SucursalId != null)
            .Select(us => us.SucursalId!.Value)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<int>> ObtenerAlmacentesPermitidosAsync(
        Guid usuarioId, CancellationToken ct = default)
    {
        if (await EsSuperAdminAsync(usuarioId, ct))
            return [];  // Vacío = sin restricción

        return await _context.UsuariosAlmacenes
            .AsNoTracking()
            .Where(ua => ua.UsuarioId == usuarioId)
            .Select(ua => ua.AlmacenId)
            .Distinct()
            .ToListAsync(ct);
    }
}

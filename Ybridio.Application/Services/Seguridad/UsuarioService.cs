using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Core;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.Domain.Seguridad;
using Ybridio.Infrastructure.Persistence;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Application.Services.Seguridad;

public sealed class UsuarioService : IUsuarioService
{
    private readonly ErpDbContext _context;
    private readonly UserManager<ApplicationUser> _users;

    public UsuarioService(ErpDbContext context, UserManager<ApplicationUser> users)
    {
        _context = context;
        _users   = users;
    }

    public async Task<IReadOnlyList<UsuarioDto>> ListarPorEmpresaAsync(
        int empresaId, CancellationToken ct = default)
    {
        // El global filter ya filtra por EmpresaId; el parámetro es redundante pero explícito
        var lista = await _context.Users
            .AsNoTracking()
            .Where(u => u.EmpresaId == empresaId)
            .OrderBy(u => u.Nombre)
            .ToListAsync(ct);

        return lista.Select(MapToDto).ToList();
    }

    public async Task<ServiceResult<UsuarioDto>> CrearAsync(
        CrearUsuarioDto dto, CancellationToken ct = default)
    {
        var user = new ApplicationUser
        {
            EmpresaId     = dto.EmpresaId,
            Nombre        = dto.Nombre.Trim(),
            UserName      = dto.UserName.Trim(),
            Email         = dto.Email.Trim(),
            Activo        = true,
            FechaCreacion = DateTime.UtcNow,
            Borrado       = false
        };

        var result = await _users.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            var error = string.Join("; ", result.Errors.Select(e => e.Description));
            return ServiceResult<UsuarioDto>.Fail(error, ErrorCode.ValidationFailed);
        }

        return ServiceResult<UsuarioDto>.Ok(MapToDto(user));
    }

    public async Task<ServiceResult<UsuarioDto>> ActualizarAsync(
        Guid usuarioId, ActualizarUsuarioDto dto, Guid modificadoPor, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(usuarioId.ToString());
        if (user is null)
            return ServiceResult<UsuarioDto>.Fail("Usuario no encontrado.", ErrorCode.NotFound);

        user.Nombre              = dto.Nombre.Trim();
        user.Email               = dto.Email?.Trim();
        user.UserName            = dto.Email?.Trim() ?? user.UserName;
        user.Activo              = dto.Activo;
        user.FechaModificacion   = DateTime.UtcNow;
        user.UsuarioModificacionId = modificadoPor;

        var result = await _users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var error = string.Join("; ", result.Errors.Select(e => e.Description));
            return ServiceResult<UsuarioDto>.Fail(error, ErrorCode.ValidationFailed);
        }

        return ServiceResult<UsuarioDto>.Ok(MapToDto(user));
    }

    public async Task<ServiceResult> CambiarActivoAsync(
        Guid usuarioId, bool activo, Guid modificadoPor, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(usuarioId.ToString());
        if (user is null)
            return ServiceResult.Fail("Usuario no encontrado.", ErrorCode.NotFound);

        user.Activo              = activo;
        user.FechaModificacion   = DateTime.UtcNow;
        user.UsuarioModificacionId = modificadoPor;

        var result = await _users.UpdateAsync(user);
        return result.Succeeded
            ? ServiceResult.Ok()
            : ServiceResult.Fail(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    // ── Sucursales ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SucursalDto>> ListarSucursalesAsync(
        Guid usuarioId, CancellationToken ct = default)
    {
        var ids = await _context.UsuariosSucursales
            .AsNoTracking()
            .Where(ut => ut.UsuarioId == usuarioId && ut.SucursalId.HasValue)
            .Select(ut => ut.SucursalId!.Value)
            .Distinct()
            .ToListAsync(ct);

        if (ids.Count == 0) return [];

        var tiendas = await _context.Sucursales
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .OrderBy(t => t.Nombre)
            .ToListAsync(ct);

        return tiendas.Select(t => new SucursalDto(t.Id, t.EmpresaId, t.Nombre)).ToList();
    }

    public async Task<ServiceResult> AsignarSucursalesAsync(
        Guid usuarioId, IReadOnlyList<int> sucursalIds, CancellationToken ct = default)
    {
        var existentes = await _context.UsuariosSucursales
            .Where(ut => ut.UsuarioId == usuarioId)
            .ToListAsync(ct);

        _context.UsuariosSucursales.RemoveRange(existentes);

        foreach (var id in sucursalIds)
        {
            _context.UsuariosSucursales.Add(new UsuarioSucursal
            {
                UsuarioId = usuarioId,
                SucursalId  = id
            });
        }

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // ── Roles ─────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<string>> ListarRolesAsync(
        Guid usuarioId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(usuarioId.ToString());
        if (user is null) return [];

        var roles = await _users.GetRolesAsync(user);
        return roles.OrderBy(r => r).ToList();
    }

    public async Task<ServiceResult> AsignarRolesAsync(
        Guid usuarioId, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(usuarioId.ToString());
        if (user is null)
            return ServiceResult.Fail("Usuario no encontrado.", ErrorCode.NotFound);

        var actuales = await _users.GetRolesAsync(user);

        var quitar   = actuales.Except(roles).ToList();
        var agregar  = roles.Except(actuales).ToList();

        if (quitar.Count > 0)
        {
            var r = await _users.RemoveFromRolesAsync(user, quitar);
            if (!r.Succeeded)
                return ServiceResult.Fail(string.Join("; ", r.Errors.Select(e => e.Description)));
        }
        if (agregar.Count > 0)
        {
            var r = await _users.AddToRolesAsync(user, agregar);
            if (!r.Succeeded)
                return ServiceResult.Fail(string.Join("; ", r.Errors.Select(e => e.Description)));
        }

        return ServiceResult.Ok();
    }

    // ── Map ───────────────────────────────────────────────────────────────────

    private static UsuarioDto MapToDto(ApplicationUser u) =>
        new(u.Id, u.EmpresaId, u.Nombre, u.UserName ?? string.Empty, u.Email, u.Activo);
}

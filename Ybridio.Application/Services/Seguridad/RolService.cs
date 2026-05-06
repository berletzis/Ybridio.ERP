using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Application.Services.Seguridad;

public sealed class RolService : IRolService
{
    private readonly RoleManager<ApplicationRole> _roles;

    public RolService(RoleManager<ApplicationRole> roles) => _roles = roles;

    public async Task<IReadOnlyList<RolDto>> ListarAsync(CancellationToken ct = default)
    {
        var lista = await _roles.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        return lista
            .Select(r => new RolDto(r.Id, r.Name ?? string.Empty, r.FechaCreacion))
            .ToList();
    }

    public async Task<ServiceResult<RolDto>> CrearAsync(
        string nombre, CancellationToken ct = default)
    {
        var nombre_trim = nombre.Trim();
        if (string.IsNullOrWhiteSpace(nombre_trim))
            return ServiceResult<RolDto>.Fail("El nombre del rol es obligatorio.");

        var rol = new ApplicationRole
        {
            Name         = nombre_trim,
            FechaCreacion = DateTime.UtcNow,
            Borrado       = false
        };

        var result = await _roles.CreateAsync(rol);
        if (!result.Succeeded)
        {
            var error = string.Join("; ", result.Errors.Select(e => e.Description));
            return ServiceResult<RolDto>.Fail(error, ErrorCode.ValidationFailed);
        }

        return ServiceResult<RolDto>.Ok(new RolDto(rol.Id, rol.Name!, rol.FechaCreacion));
    }

    public async Task<ServiceResult> EliminarAsync(Guid rolId, CancellationToken ct = default)
    {
        var rol = await _roles.FindByIdAsync(rolId.ToString());
        if (rol is null)
            return ServiceResult.Fail("Rol no encontrado.", ErrorCode.NotFound);

        var result = await _roles.DeleteAsync(rol);
        return result.Succeeded
            ? ServiceResult.Ok()
            : ServiceResult.Fail(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}

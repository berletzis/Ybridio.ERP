using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Directorio;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Domain.Catalogos;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Directorio;

/// <summary>
/// Gestión de Personas del directorio con enforcement de autorización runtime.
/// </summary>
public sealed class PersonaService : IPersonaService
{
    private readonly ErpDbContext             _context;
    private readonly IErpAuthorizationService _auth;

    public PersonaService(ErpDbContext context, IErpAuthorizationService auth)
    {
        _context = context;
        _auth    = auth;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersonaDto>> ListarPorEmpresaAsync(int empresaId, CancellationToken ct = default)
    {
        var lista = await _context.Personas
            .AsNoTracking()
            .Where(p => p.EmpresaId == empresaId)
            .OrderBy(p => p.Nombre).ThenBy(p => p.Apellidos)
            .ToListAsync(ct);
        return lista.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersonaDto>> BuscarAsync(int empresaId, string termino, CancellationToken ct = default)
    {
        var t = termino.Trim();
        var lista = await _context.Personas
            .AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && (
                p.Nombre.Contains(t) ||
                (p.Apellidos != null && p.Apellidos.Contains(t)) ||
                (p.RFC != null && p.RFC.Contains(t)) ||
                (p.Email != null && p.Email.Contains(t))))
            .OrderBy(p => p.Nombre).ThenBy(p => p.Apellidos)
            .Take(50)
            .ToListAsync(ct);
        return lista.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<PersonaDto>> ObtenerPorIdAsync(int personaId, CancellationToken ct = default)
    {
        var p = await _context.Personas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == personaId, ct);
        return p is null
            ? ServiceResult<PersonaDto>.Fail("Persona no encontrada.", ErrorCode.NotFound)
            : ServiceResult<PersonaDto>.Ok(MapToDto(p));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<PersonaDto>> CrearAsync(CrearPersonaDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Directorio.Editar, ct))
            return ServiceResult<PersonaDto>.Fail("Sin permiso para crear personas (directorio.editar).", ErrorCode.Unauthorized);

        var persona = new Persona
        {
            EmpresaId          = dto.EmpresaId,
            EmpresaComercialId = dto.EmpresaComercialId,
            Nombre             = dto.Nombre.Trim(),
            Apellidos          = dto.Apellidos?.Trim(),
            RFC                = dto.RFC?.Trim(),
            Email              = dto.Email?.Trim(),
            Telefono           = dto.Telefono?.Trim(),
            Direccion          = dto.Direccion?.Trim(),
            Notas              = dto.Notas?.Trim(),
            Activo             = true,
            FechaCreacion      = DateTime.UtcNow,
            UsuarioCreacionId  = usuarioId,
            Borrado            = false,
        };
        _context.Personas.Add(persona);
        await _context.SaveChangesAsync(ct);
        return ServiceResult<PersonaDto>.Ok(MapToDto(persona));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<PersonaDto>> ActualizarAsync(int personaId, ActualizarPersonaDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Directorio.Editar, ct))
            return ServiceResult<PersonaDto>.Fail("Sin permiso para editar personas (directorio.editar).", ErrorCode.Unauthorized);

        var p = await _context.Personas.FirstOrDefaultAsync(x => x.Id == personaId, ct);
        if (p is null) return ServiceResult<PersonaDto>.Fail("Persona no encontrada.", ErrorCode.NotFound);

        p.EmpresaComercialId    = dto.EmpresaComercialId;
        p.Nombre                = dto.Nombre.Trim();
        p.Apellidos             = dto.Apellidos?.Trim();
        p.RFC                   = dto.RFC?.Trim();
        p.Email                 = dto.Email?.Trim();
        p.Telefono              = dto.Telefono?.Trim();
        p.Direccion             = dto.Direccion?.Trim();
        p.Notas                 = dto.Notas?.Trim();
        p.Activo                = dto.Activo;
        p.FechaModificacion     = DateTime.UtcNow;
        p.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<PersonaDto>.Ok(MapToDto(p));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarAsync(int personaId, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Directorio.Editar, ct))
            return ServiceResult.Fail("Sin permiso para eliminar personas (directorio.editar).", ErrorCode.Unauthorized);

        var p = await _context.Personas.FirstOrDefaultAsync(x => x.Id == personaId, ct);
        if (p is null) return ServiceResult.Fail("Persona no encontrada.", ErrorCode.NotFound);

        p.Borrado               = true;
        p.Activo                = false;
        p.FechaModificacion     = DateTime.UtcNow;
        p.UsuarioModificacionId = usuarioId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static PersonaDto MapToDto(Persona p) =>
        new(p.Id, p.EmpresaId, p.EmpresaComercialId,
            p.Nombre, p.Apellidos, p.NombreCompleto,
            p.RFC, p.Email, p.Telefono, p.Direccion, p.Notas, p.Activo);
}

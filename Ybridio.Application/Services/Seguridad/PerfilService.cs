using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Seguridad;
using Ybridio.Domain.Seguridad;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Seguridad;

/// <summary>
/// Implementación de <see cref="IPerfilService"/>.
/// Gestiona perfiles de permisos reutilizables y su asignación a usuarios.
/// </summary>
public sealed class PerfilService : IPerfilService
{
    private readonly ErpDbContext _context;

    public PerfilService(ErpDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PerfilDto>> ListarAsync(CancellationToken ct = default)
    {
        var lista = await _context.Perfiles
            .AsNoTracking()
            .Include(p => p.PerfilPermisos)
            .OrderBy(p => p.Nombre)
            .ToListAsync(ct);

        return lista.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<PerfilDto?> ObtenerPorIdAsync(int perfilId, CancellationToken ct = default)
    {
        var perfil = await _context.Perfiles
            .AsNoTracking()
            .Include(p => p.PerfilPermisos)
            .FirstOrDefaultAsync(p => p.Id == perfilId, ct);

        return perfil is null ? null : MapToDto(perfil);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<PerfilDto>> CrearAsync(CrearPerfilDto dto, CancellationToken ct = default)
    {
        var nombre = dto.Nombre.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
            return ServiceResult<PerfilDto>.Fail("El nombre del perfil es obligatorio.");

        var existe = await _context.Perfiles.AnyAsync(p => p.Nombre == nombre, ct);
        if (existe)
            return ServiceResult<PerfilDto>.Fail($"Ya existe un perfil con el nombre '{nombre}'.");

        var perfil = new Perfil
        {
            Nombre            = nombre,
            Descripcion       = dto.Descripcion?.Trim(),
            Activo            = true,
            FechaCreacion     = DateTime.UtcNow,
            UsuarioCreacionId = dto.UsuarioCreacionId,
            Borrado           = false,
        };

        _context.Perfiles.Add(perfil);
        await _context.SaveChangesAsync(ct);

        if (dto.PermisoIds.Count > 0)
        {
            var join = dto.PermisoIds.Select(pid => new PerfilPermiso
            {
                PerfilId  = perfil.Id,
                PermisoId = pid,
            });
            _context.PerfilPermisos.AddRange(join);
            await _context.SaveChangesAsync(ct);
        }

        return ServiceResult<PerfilDto>.Ok(new PerfilDto(
            perfil.Id, perfil.Nombre, perfil.Descripcion, perfil.Activo, dto.PermisoIds.Count));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<PerfilDto>> ActualizarAsync(
        int perfilId, ActualizarPerfilDto dto, CancellationToken ct = default)
    {
        var perfil = await _context.Perfiles
            .Include(p => p.PerfilPermisos)
            .FirstOrDefaultAsync(p => p.Id == perfilId, ct);

        if (perfil is null)
            return ServiceResult<PerfilDto>.Fail("Perfil no encontrado.", ErrorCode.NotFound);

        var nombre = dto.Nombre.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
            return ServiceResult<PerfilDto>.Fail("El nombre del perfil es obligatorio.");

        var duplicado = await _context.Perfiles
            .AnyAsync(p => p.Nombre == nombre && p.Id != perfilId, ct);
        if (duplicado)
            return ServiceResult<PerfilDto>.Fail($"Ya existe otro perfil con el nombre '{nombre}'.");

        perfil.Nombre                = nombre;
        perfil.Descripcion           = dto.Descripcion?.Trim();
        perfil.Activo                = dto.Activo;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<PerfilDto>.Ok(MapToDto(perfil));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> AsignarPermisosAsync(
        int perfilId, IReadOnlyList<int> permisoIds, CancellationToken ct = default)
    {
        var perfil = await _context.Perfiles
            .Include(p => p.PerfilPermisos)
            .FirstOrDefaultAsync(p => p.Id == perfilId, ct);

        if (perfil is null)
            return ServiceResult.Fail("Perfil no encontrado.", ErrorCode.NotFound);

        // Reemplazar permisos: quitar los que sobran, agregar los nuevos
        var actualesIds  = perfil.PerfilPermisos.Select(pp => pp.PermisoId).ToHashSet();
        var nuevosIds    = permisoIds.ToHashSet();

        var quitar = perfil.PerfilPermisos.Where(pp => !nuevosIds.Contains(pp.PermisoId)).ToList();
        _context.PerfilPermisos.RemoveRange(quitar);

        var agregar = nuevosIds
            .Except(actualesIds)
            .Select(pid => new PerfilPermiso { PerfilId = perfilId, PermisoId = pid });
        _context.PerfilPermisos.AddRange(agregar);

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> AsignarAUsuarioAsync(
        int perfilId, Guid usuarioId, CancellationToken ct = default)
    {
        var ya = await _context.UsuariosPerfiles
            .AnyAsync(up => up.UsuarioId == usuarioId && up.PerfilId == perfilId, ct);
        if (ya)
            return ServiceResult.Ok();

        _context.UsuariosPerfiles.Add(new UsuarioPerfil
        {
            UsuarioId = usuarioId,
            PerfilId  = perfilId,
        });
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> QuitarDeUsuarioAsync(
        int perfilId, Guid usuarioId, CancellationToken ct = default)
    {
        var asignacion = await _context.UsuariosPerfiles
            .FirstOrDefaultAsync(up => up.UsuarioId == usuarioId && up.PerfilId == perfilId, ct);

        if (asignacion is null)
            return ServiceResult.Ok();

        _context.UsuariosPerfiles.Remove(asignacion);
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PerfilDto>> ListarPorUsuarioAsync(
        Guid usuarioId, CancellationToken ct = default)
    {
        var perfiles = await _context.UsuariosPerfiles
            .AsNoTracking()
            .Where(up => up.UsuarioId == usuarioId)
            .Select(up => up.Perfil)
            .Include(p => p.PerfilPermisos)
            .OrderBy(p => p.Nombre)
            .ToListAsync(ct);

        return perfiles.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarAsync(int perfilId, Guid usuarioModificacionId, CancellationToken ct = default)
    {
        var perfil = await _context.Perfiles.FirstOrDefaultAsync(p => p.Id == perfilId, ct);
        if (perfil is null)
            return ServiceResult.Fail("Perfil no encontrado.", ErrorCode.NotFound);

        perfil.Borrado = true;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static PerfilDto MapToDto(Perfil p) =>
        new(p.Id, p.Nombre, p.Descripcion, p.Activo, p.PerfilPermisos.Count);
}

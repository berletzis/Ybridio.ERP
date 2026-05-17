using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Inventario;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Inventario;

/// <summary>
/// Implementación de <see cref="IAlmacenService"/> sobre EF Core.
/// Todas las queries son AsNoTracking (solo lectura).
/// </summary>
public sealed class AlmacenService : IAlmacenService
{
    private readonly ErpDbContext _context;

    public AlmacenService(ErpDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task<ServiceResult<AlmacenDto>> ObtenerPrincipalDeSucursalAsync(
        int sucursalId, CancellationToken ct = default)
    {
        var almacen = await _context.Almacenes
            .AsNoTracking()
            .Where(a => a.SucursalId == sucursalId
                     && a.EsPrincipal
                     && !a.Borrado
                     && a.Activo)
            .FirstOrDefaultAsync(ct);

        if (almacen is null)
            return ServiceResult<AlmacenDto>.Fail(
                $"La sucursal {sucursalId} no tiene un almacén principal configurado. " +
                "Ve a Configuración → Sucursal → Almacenes y marca uno como principal.",
                ErrorCode.NotFound);

        return ServiceResult<AlmacenDto>.Ok(MapToDto(almacen));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AlmacenDto>> ListarPorSucursalAsync(
        int sucursalId, CancellationToken ct = default)
    {
        var lista = await _context.Almacenes
            .AsNoTracking()
            .Where(a => a.SucursalId == sucursalId && !a.Borrado)
            .OrderByDescending(a => a.EsPrincipal)
            .ThenBy(a => a.Nombre)
            .ToListAsync(ct);

        return lista.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<AlmacenDto>> CrearAsync(
        CrearAlmacenDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return ServiceResult<AlmacenDto>.Fail("El nombre del almacén es obligatorio.");

        if (!string.IsNullOrWhiteSpace(dto.Codigo))
        {
            var codigoEnUso = await _context.Almacenes.AnyAsync(
                a => a.SucursalId == dto.SucursalId
                  && a.Codigo == dto.Codigo.Trim()
                  && !a.Borrado, ct);
            if (codigoEnUso)
                return ServiceResult<AlmacenDto>.Fail(
                    $"Ya existe un almacén con el código '{dto.Codigo}' en esta sucursal.");
        }

        var almacen = new Domain.Inventario.Almacen
        {
            EmpresaId         = dto.EmpresaId,
            SucursalId        = dto.SucursalId,
            Nombre            = dto.Nombre.Trim(),
            Codigo            = dto.Codigo?.Trim(),
            Descripcion       = dto.Descripcion?.Trim(),
            Activo            = true,
            EsPrincipal       = false,
            FechaCreacion     = DateTime.UtcNow,
            UsuarioCreacionId = usuarioId,
            Borrado           = false,
        };

        _context.Almacenes.Add(almacen);
        await _context.SaveChangesAsync(ct);
        return ServiceResult<AlmacenDto>.Ok(MapToDto(almacen));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<AlmacenDto>> ActualizarAsync(
        int id, ActualizarAlmacenDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return ServiceResult<AlmacenDto>.Fail("El nombre del almacén es obligatorio.");

        var almacen = await _context.Almacenes
            .FirstOrDefaultAsync(a => a.Id == id && !a.Borrado, ct);
        if (almacen is null)
            return ServiceResult<AlmacenDto>.Fail("Almacén no encontrado.", ErrorCode.NotFound);

        almacen.Nombre                = dto.Nombre.Trim();
        almacen.Codigo                = dto.Codigo?.Trim();
        almacen.Descripcion           = dto.Descripcion?.Trim();
        almacen.FechaModificacion     = DateTime.UtcNow;
        almacen.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<AlmacenDto>.Ok(MapToDto(almacen));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> MarcarPrincipalAsync(
        int almacenId, Guid usuarioId, CancellationToken ct = default)
    {
        var almacen = await _context.Almacenes
            .FirstOrDefaultAsync(a => a.Id == almacenId && !a.Borrado, ct);
        if (almacen is null)
            return ServiceResult.Fail("Almacén no encontrado.", ErrorCode.NotFound);

        if (!almacen.Activo)
            return ServiceResult.Fail("No se puede marcar como principal un almacén inactivo.");

        var principalesActuales = await _context.Almacenes
            .Where(a => a.SucursalId == almacen.SucursalId && a.EsPrincipal && !a.Borrado)
            .ToListAsync(ct);
        foreach (var p in principalesActuales)
            p.EsPrincipal = false;

        almacen.EsPrincipal           = true;
        almacen.FechaModificacion     = DateTime.UtcNow;
        almacen.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> CambiarActivoAsync(
        int almacenId, Guid usuarioId, CancellationToken ct = default)
    {
        var almacen = await _context.Almacenes
            .FirstOrDefaultAsync(a => a.Id == almacenId && !a.Borrado, ct);
        if (almacen is null)
            return ServiceResult.Fail("Almacén no encontrado.", ErrorCode.NotFound);

        if (almacen.EsPrincipal && almacen.Activo)
            return ServiceResult.Fail(
                "No se puede desactivar el almacén principal. " +
                "Marca otro almacén como principal primero.");

        almacen.Activo                = !almacen.Activo;
        almacen.FechaModificacion     = DateTime.UtcNow;
        almacen.UsuarioModificacionId = usuarioId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarAsync(
        int almacenId, Guid usuarioId, CancellationToken ct = default)
    {
        var almacen = await _context.Almacenes
            .FirstOrDefaultAsync(a => a.Id == almacenId && !a.Borrado, ct);
        if (almacen is null)
            return ServiceResult.Fail("Almacén no encontrado.", ErrorCode.NotFound);

        if (almacen.EsPrincipal)
            return ServiceResult.Fail(
                "No se puede eliminar el almacén principal. " +
                "Marca otro almacén como principal primero.");

        almacen.Borrado               = true;
        almacen.FechaModificacion     = DateTime.UtcNow;
        almacen.UsuarioModificacionId = usuarioId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static AlmacenDto MapToDto(Domain.Inventario.Almacen a) =>
        new(a.Id, a.EmpresaId, a.SucursalId, a.Codigo,
            a.Nombre, a.Descripcion, a.Activo, a.EsPrincipal);
}

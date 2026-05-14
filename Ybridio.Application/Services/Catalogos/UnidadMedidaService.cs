using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.Domain.Catalogos;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Catalogos;

/// <summary>Implementación de <see cref="IUnidadMedidaService"/> sobre EF Core.</summary>
public sealed class UnidadMedidaService : IUnidadMedidaService
{
    private readonly ErpDbContext _context;
    private readonly ISessionContext _session;

    public UnidadMedidaService(ErpDbContext context, ISessionContext session)
    {
        _context = context;
        _session = session;
    }

    public async Task<IReadOnlyList<UnidadMedidaDto>> ListarAsync(CancellationToken ct = default)
    {
        var lista = await _context.UnidadesMedida
            .AsNoTracking()
            .Where(u => u.EmpresaId == _session.EmpresaId && u.Activo)
            .OrderBy(u => u.Nombre)
            .ToListAsync(ct);

        return lista.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<UnidadMedidaDto>> ListarTodosAsync(CancellationToken ct = default)
    {
        var lista = await _context.UnidadesMedida
            .AsNoTracking()
            .Where(u => u.EmpresaId == _session.EmpresaId)
            .OrderBy(u => u.Nombre)
            .ToListAsync(ct);

        return lista.Select(MapToDto).ToList();
    }

    public async Task<ServiceResult<UnidadMedidaDto>> GuardarAsync(
        int id, UpsertUnidadMedidaDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return ServiceResult<UnidadMedidaDto>.Fail("El nombre de la unidad de medida es requerido.");

        if (string.IsNullOrWhiteSpace(dto.Abreviatura))
            return ServiceResult<UnidadMedidaDto>.Fail("La abreviatura es requerida.");

        var usuarioId = _session.UsuarioId ?? Guid.Empty;

        if (id == 0)
        {
            var nueva = new UnidadMedida
            {
                EmpresaId         = _session.EmpresaId,
                Nombre            = dto.Nombre.Trim(),
                Abreviatura       = dto.Abreviatura.Trim(),
                Activo            = dto.Activo,
                FechaCreacion     = DateTime.UtcNow,
                UsuarioCreacionId = usuarioId,
            };

            _context.UnidadesMedida.Add(nueva);
            await _context.SaveChangesAsync(ct);
            return ServiceResult<UnidadMedidaDto>.Ok(MapToDto(nueva));
        }
        else
        {
            var unidad = await _context.UnidadesMedida
                .FirstOrDefaultAsync(u => u.Id == id && u.EmpresaId == _session.EmpresaId, ct);

            if (unidad is null)
                return ServiceResult<UnidadMedidaDto>.Fail("Unidad de medida no encontrada.", ErrorCode.NotFound);

            unidad.Nombre               = dto.Nombre.Trim();
            unidad.Abreviatura          = dto.Abreviatura.Trim();
            unidad.Activo               = dto.Activo;
            unidad.FechaModificacion    = DateTime.UtcNow;
            unidad.UsuarioModificacionId = usuarioId;

            await _context.SaveChangesAsync(ct);
            return ServiceResult<UnidadMedidaDto>.Ok(MapToDto(unidad));
        }
    }

    public async Task<ServiceResult> EliminarAsync(int id, CancellationToken ct = default)
    {
        var unidad = await _context.UnidadesMedida
            .FirstOrDefaultAsync(u => u.Id == id && u.EmpresaId == _session.EmpresaId, ct);

        if (unidad is null)
            return ServiceResult.Fail("Unidad de medida no encontrada.", ErrorCode.NotFound);

        unidad.Borrado               = true;
        unidad.FechaModificacion     = DateTime.UtcNow;
        unidad.UsuarioModificacionId = _session.UsuarioId ?? Guid.Empty;

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static UnidadMedidaDto MapToDto(UnidadMedida u) =>
        new(u.Id, u.EmpresaId, u.Nombre, u.Abreviatura, u.Activo);
}

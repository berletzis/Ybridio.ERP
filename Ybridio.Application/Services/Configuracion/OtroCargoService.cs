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

namespace Ybridio.Application.Services.Configuracion;

/// <summary>
/// Implementación de <see cref="IOtroCargoService"/> sobre EF Core.
/// </summary>
public sealed class OtroCargoService : IOtroCargoService
{
    private readonly ErpDbContext _context;
    private readonly ISessionContext _session;

    public OtroCargoService(ErpDbContext context, ISessionContext session)
    {
        _context = context;
        _session = session;
    }

    public async Task<IReadOnlyList<OtroCargoDto>> ListarAsync(CancellationToken ct = default)
    {
        var lista = await _context.OtrosCargos
            .AsNoTracking()
            .Include(c => c.TipoImpuesto)
            .Where(c => c.EmpresaId == _session.EmpresaId && c.Activo)
            .OrderBy(c => c.OrdenVisual)
            .ThenBy(c => c.Nombre)
            .ToListAsync(ct);

        return lista.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<OtroCargoDto>> ListarTodosAsync(CancellationToken ct = default)
    {
        var lista = await _context.OtrosCargos
            .AsNoTracking()
            .Include(c => c.TipoImpuesto)
            .Where(c => c.EmpresaId == _session.EmpresaId)
            .OrderBy(c => c.OrdenVisual)
            .ThenBy(c => c.Nombre)
            .ToListAsync(ct);

        return lista.Select(MapToDto).ToList();
    }

    public async Task<ServiceResult<OtroCargoDto>> GuardarAsync(
        GuardarOtroCargoDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Codigo))
            return ServiceResult<OtroCargoDto>.Fail("El código del cargo es requerido.");

        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return ServiceResult<OtroCargoDto>.Fail("El nombre del cargo es requerido.");

        var usuarioId = _session.UsuarioId ?? Guid.Empty;

        if (dto.Id == 0)
        {
            var existe = await _context.OtrosCargos.AnyAsync(
                c => c.EmpresaId == _session.EmpresaId && c.Codigo == dto.Codigo.Trim(), ct);

            if (existe)
                return ServiceResult<OtroCargoDto>.Fail($"Ya existe un cargo con el código '{dto.Codigo}'.");

            var nuevo = new OtroCargo
            {
                EmpresaId         = _session.EmpresaId,
                Codigo            = dto.Codigo.Trim().ToUpperInvariant(),
                Nombre            = dto.Nombre.Trim(),
                TipoCargo         = string.IsNullOrWhiteSpace(dto.TipoCargo) ? "Otro" : dto.TipoCargo.Trim(),
                AplicaIva         = dto.AplicaIva,
                TipoImpuestoId    = dto.TipoImpuestoId,
                OrdenVisual       = dto.OrdenVisual,
                Activo            = dto.Activo,
                FechaCreacion     = DateTime.UtcNow,
                UsuarioCreacionId = usuarioId,
            };

            _context.OtrosCargos.Add(nuevo);
            await _context.SaveChangesAsync(ct);

            await _context.Entry(nuevo).Reference(c => c.TipoImpuesto).LoadAsync(ct);
            return ServiceResult<OtroCargoDto>.Ok(MapToDto(nuevo));
        }
        else
        {
            var cargo = await _context.OtrosCargos
                .FirstOrDefaultAsync(c => c.Id == dto.Id && c.EmpresaId == _session.EmpresaId, ct);

            if (cargo is null)
                return ServiceResult<OtroCargoDto>.Fail("Cargo no encontrado.", ErrorCode.NotFound);

            // Verificar unicidad de código si cambió
            if (!string.Equals(cargo.Codigo, dto.Codigo.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var codigoEnUso = await _context.OtrosCargos.AnyAsync(
                    c => c.EmpresaId == _session.EmpresaId && c.Codigo == dto.Codigo.Trim() && c.Id != dto.Id, ct);
                if (codigoEnUso)
                    return ServiceResult<OtroCargoDto>.Fail($"Ya existe un cargo con el código '{dto.Codigo}'.");
            }

            cargo.Codigo                = dto.Codigo.Trim().ToUpperInvariant();
            cargo.Nombre                = dto.Nombre.Trim();
            cargo.TipoCargo             = string.IsNullOrWhiteSpace(dto.TipoCargo) ? "Otro" : dto.TipoCargo.Trim();
            cargo.AplicaIva             = dto.AplicaIva;
            cargo.TipoImpuestoId        = dto.TipoImpuestoId;
            cargo.OrdenVisual           = dto.OrdenVisual;
            cargo.Activo                = dto.Activo;
            cargo.FechaModificacion     = DateTime.UtcNow;
            cargo.UsuarioModificacionId = usuarioId;

            await _context.SaveChangesAsync(ct);

            await _context.Entry(cargo).Reference(c => c.TipoImpuesto).LoadAsync(ct);
            return ServiceResult<OtroCargoDto>.Ok(MapToDto(cargo));
        }
    }

    public async Task<ServiceResult> EliminarAsync(int id, CancellationToken ct = default)
    {
        var cargo = await _context.OtrosCargos
            .FirstOrDefaultAsync(c => c.Id == id && c.EmpresaId == _session.EmpresaId, ct);

        if (cargo is null)
            return ServiceResult.Fail("Cargo no encontrado.", ErrorCode.NotFound);

        cargo.Borrado               = true;
        cargo.FechaModificacion     = DateTime.UtcNow;
        cargo.UsuarioModificacionId = _session.UsuarioId ?? Guid.Empty;

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static OtroCargoDto MapToDto(OtroCargo c) => new(
        c.Id, c.Codigo, c.Nombre, c.TipoCargo, c.AplicaIva,
        c.TipoImpuestoId, c.TipoImpuesto?.Nombre,
        c.OrdenVisual, c.Activo);
}

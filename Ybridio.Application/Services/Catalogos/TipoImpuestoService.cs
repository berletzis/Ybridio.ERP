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

/// <summary>
/// Implementación de <see cref="ITipoImpuestoService"/> sobre EF Core.
/// Gestiona el catálogo fiscal institucional — única fuente de verdad sobre impuestos.
/// </summary>
public sealed class TipoImpuestoService : ITipoImpuestoService
{
    private readonly ErpDbContext _context;
    private readonly ISessionContext _session;

    public TipoImpuestoService(ErpDbContext context, ISessionContext session)
    {
        _context = context;
        _session = session;
    }

    public async Task<IReadOnlyList<TipoImpuestoDto>> ListarAsync(CancellationToken ct = default)
    {
        var lista = await _context.TiposImpuesto
            .AsNoTracking()
            .Where(t => t.EmpresaId == _session.EmpresaId && t.Activo)
            .OrderBy(t => t.OrdenVisual)
            .ThenBy(t => t.Nombre)
            .ToListAsync(ct);

        return lista.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<TipoImpuestoDto>> ListarTodosAsync(CancellationToken ct = default)
    {
        var lista = await _context.TiposImpuesto
            .AsNoTracking()
            .Where(t => t.EmpresaId == _session.EmpresaId)
            .OrderBy(t => t.OrdenVisual)
            .ThenBy(t => t.Nombre)
            .ToListAsync(ct);

        return lista.Select(MapToDto).ToList();
    }

    public async Task<ServiceResult<TipoImpuestoDto>> GuardarAsync(
        int id, UpsertTipoImpuestoDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return ServiceResult<TipoImpuestoDto>.Fail("El nombre del tipo de impuesto es requerido.");

        if (dto.Porcentaje < 0 || dto.Porcentaje > 100)
            return ServiceResult<TipoImpuestoDto>.Fail("El porcentaje debe estar entre 0 y 100.");

        // EsExento: derivado automáticamente de TipoGravamen
        var esExento = dto.Gravamen == TipoGravamen.Exento;

        // Si es exento, porcentaje debe ser 0
        if (esExento && dto.Porcentaje != 0)
            return ServiceResult<TipoImpuestoDto>.Fail("Los impuestos Exentos deben tener porcentaje 0.");

        var usuarioId = _session.UsuarioId ?? Guid.Empty;
        var codigo    = dto.Codigo.Trim().ToUpperInvariant();

        if (id == 0)
        {
            // Verificar código único si no está vacío
            if (!string.IsNullOrEmpty(codigo))
            {
                var codigoDuplicado = await _context.TiposImpuesto.AnyAsync(
                    t => t.EmpresaId == _session.EmpresaId && t.Codigo == codigo, ct);
                if (codigoDuplicado)
                    return ServiceResult<TipoImpuestoDto>.Fail($"El código '{codigo}' ya está en uso.");
            }

            var nuevo = new TipoImpuesto
            {
                EmpresaId         = _session.EmpresaId,
                Nombre            = dto.Nombre.Trim(),
                Codigo            = codigo,
                Porcentaje        = dto.Porcentaje,
                TipoGravamen      = dto.Gravamen,
                EsExento          = esExento,
                Descripcion       = dto.Descripcion?.Trim(),
                OrdenVisual       = dto.OrdenVisual,
                Activo            = dto.Activo,
                FechaCreacion     = DateTime.UtcNow,
                UsuarioCreacionId = usuarioId,
            };

            _context.TiposImpuesto.Add(nuevo);
            await _context.SaveChangesAsync(ct);
            return ServiceResult<TipoImpuestoDto>.Ok(MapToDto(nuevo));
        }
        else
        {
            var tipo = await _context.TiposImpuesto
                .FirstOrDefaultAsync(t => t.Id == id && t.EmpresaId == _session.EmpresaId, ct);

            if (tipo is null)
                return ServiceResult<TipoImpuestoDto>.Fail("Tipo de impuesto no encontrado.", ErrorCode.NotFound);

            // Verificar unicidad de código si cambió
            if (!string.IsNullOrEmpty(codigo) &&
                !string.Equals(tipo.Codigo, codigo, StringComparison.Ordinal))
            {
                var codigoDuplicado = await _context.TiposImpuesto.AnyAsync(
                    t => t.EmpresaId == _session.EmpresaId && t.Codigo == codigo && t.Id != id, ct);
                if (codigoDuplicado)
                    return ServiceResult<TipoImpuestoDto>.Fail($"El código '{codigo}' ya está en uso.");
            }

            tipo.Nombre               = dto.Nombre.Trim();
            tipo.Codigo               = codigo;
            tipo.Porcentaje           = dto.Porcentaje;
            tipo.TipoGravamen         = dto.Gravamen;
            tipo.EsExento             = esExento;
            tipo.Descripcion          = dto.Descripcion?.Trim();
            tipo.OrdenVisual          = dto.OrdenVisual;
            tipo.Activo               = dto.Activo;
            tipo.FechaModificacion    = DateTime.UtcNow;
            tipo.UsuarioModificacionId = usuarioId;

            await _context.SaveChangesAsync(ct);
            return ServiceResult<TipoImpuestoDto>.Ok(MapToDto(tipo));
        }
    }

    public async Task<ServiceResult> EliminarAsync(int id, CancellationToken ct = default)
    {
        var tipo = await _context.TiposImpuesto
            .FirstOrDefaultAsync(t => t.Id == id && t.EmpresaId == _session.EmpresaId, ct);

        if (tipo is null)
            return ServiceResult.Fail("Tipo de impuesto no encontrado.", ErrorCode.NotFound);

        tipo.Borrado               = true;
        tipo.FechaModificacion     = DateTime.UtcNow;
        tipo.UsuarioModificacionId = _session.UsuarioId ?? Guid.Empty;

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static TipoImpuestoDto MapToDto(TipoImpuesto t) =>
        new(t.Id, t.EmpresaId, t.Nombre, t.Porcentaje, t.Activo,
            t.Codigo, t.TipoGravamen, t.EsExento, t.OrdenVisual, t.Descripcion);
}

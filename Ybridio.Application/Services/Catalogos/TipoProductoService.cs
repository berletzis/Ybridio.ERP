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
/// Implementación de <see cref="ITipoProductoService"/> sobre EF Core.
/// Product Type Classification Pattern: la Clave es el identificador operacional (PROD, SERV, REF…).
/// </summary>
public sealed class TipoProductoService : ITipoProductoService
{
    private readonly ErpDbContext _context;
    private readonly ISessionContext _session;

    public TipoProductoService(ErpDbContext context, ISessionContext session)
    {
        _context = context;
        _session = session;
    }

    public async Task<IReadOnlyList<TipoProductoDto>> ListarAsync(CancellationToken ct = default)
    {
        var lista = await _context.TiposProducto
            .AsNoTracking()
            .Where(t => t.EmpresaId == _session.EmpresaId && t.Activo)
            .OrderBy(t => t.OrdenVisual)
            .ThenBy(t => t.Nombre)
            .ToListAsync(ct);

        return lista.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<TipoProductoDto>> ListarTodosAsync(CancellationToken ct = default)
    {
        var lista = await _context.TiposProducto
            .AsNoTracking()
            .Where(t => t.EmpresaId == _session.EmpresaId)
            .OrderBy(t => t.OrdenVisual)
            .ThenBy(t => t.Nombre)
            .ToListAsync(ct);

        return lista.Select(MapToDto).ToList();
    }

    public async Task<ServiceResult<TipoProductoDto>> GuardarAsync(
        int id, UpsertTipoProductoDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return ServiceResult<TipoProductoDto>.Fail("El nombre del tipo de producto es requerido.");

        var clave = dto.Clave.Trim().ToUpperInvariant();
        var usuarioId = _session.UsuarioId ?? Guid.Empty;

        if (id == 0)
        {
            // Verificar Clave única si no está vacía
            if (!string.IsNullOrEmpty(clave))
            {
                var claveExiste = await _context.TiposProducto.AnyAsync(
                    t => t.EmpresaId == _session.EmpresaId && t.Clave == clave, ct);
                if (claveExiste)
                    return ServiceResult<TipoProductoDto>.Fail($"La clave '{clave}' ya está en uso.");
            }

            var nuevo = new TipoProducto
            {
                EmpresaId         = _session.EmpresaId,
                Clave             = clave,
                Nombre            = dto.Nombre.Trim(),
                Descripcion       = dto.Descripcion?.Trim(),
                OrdenVisual       = dto.OrdenVisual,
                Activo            = dto.Activo,
                FechaCreacion     = DateTime.UtcNow,
                UsuarioCreacionId = usuarioId,
            };

            _context.TiposProducto.Add(nuevo);
            await _context.SaveChangesAsync(ct);
            return ServiceResult<TipoProductoDto>.Ok(MapToDto(nuevo));
        }
        else
        {
            var tipo = await _context.TiposProducto
                .FirstOrDefaultAsync(t => t.Id == id && t.EmpresaId == _session.EmpresaId, ct);

            if (tipo is null)
                return ServiceResult<TipoProductoDto>.Fail("Tipo de producto no encontrado.", ErrorCode.NotFound);

            // Verificar Clave única si cambió
            if (!string.IsNullOrEmpty(clave) &&
                !string.Equals(tipo.Clave, clave, StringComparison.Ordinal))
            {
                var claveExiste = await _context.TiposProducto.AnyAsync(
                    t => t.EmpresaId == _session.EmpresaId && t.Clave == clave && t.Id != id, ct);
                if (claveExiste)
                    return ServiceResult<TipoProductoDto>.Fail($"La clave '{clave}' ya está en uso.");
            }

            tipo.Clave               = clave;
            tipo.Nombre              = dto.Nombre.Trim();
            tipo.Descripcion         = dto.Descripcion?.Trim();
            tipo.OrdenVisual         = dto.OrdenVisual;
            tipo.Activo              = dto.Activo;
            tipo.FechaModificacion   = DateTime.UtcNow;
            tipo.UsuarioModificacionId = usuarioId;

            await _context.SaveChangesAsync(ct);
            return ServiceResult<TipoProductoDto>.Ok(MapToDto(tipo));
        }
    }

    public async Task<ServiceResult> EliminarAsync(int id, CancellationToken ct = default)
    {
        var tipo = await _context.TiposProducto
            .FirstOrDefaultAsync(t => t.Id == id && t.EmpresaId == _session.EmpresaId, ct);

        if (tipo is null)
            return ServiceResult.Fail("Tipo de producto no encontrado.", ErrorCode.NotFound);

        tipo.Borrado               = true;
        tipo.FechaModificacion     = DateTime.UtcNow;
        tipo.UsuarioModificacionId = _session.UsuarioId ?? Guid.Empty;

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static TipoProductoDto MapToDto(TipoProducto t) =>
        new(t.Id, t.EmpresaId, t.Nombre, t.Descripcion, t.Activo, t.Clave, t.OrdenVisual);
}

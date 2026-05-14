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

/// <summary>Implementación de <see cref="ISerieDocumentoService"/> sobre EF Core.</summary>
public sealed class SerieDocumentoService : ISerieDocumentoService
{
    private readonly ErpDbContext _context;
    private readonly ISessionContext _session;

    // Lookup estático para nombres legibles del enum
    private static readonly IReadOnlyDictionary<TipoDocumentoSerie, string> _nombres =
        new Dictionary<TipoDocumentoSerie, string>
        {
            [TipoDocumentoSerie.Cotizacion]       = "Cotización",
            [TipoDocumentoSerie.Pedido]           = "Pedido",
            [TipoDocumentoSerie.Venta]            = "Venta",
            [TipoDocumentoSerie.OrdenTrabajo]     = "Orden de Trabajo",
            [TipoDocumentoSerie.EntradaAlmacen]   = "Entrada de Almacén",
            [TipoDocumentoSerie.SalidaAlmacen]    = "Salida de Almacén",
            [TipoDocumentoSerie.OrdenCompra]      = "Orden de Compra",
            [TipoDocumentoSerie.ConteoInventario] = "Conteo de Inventario",
            [TipoDocumentoSerie.Traspaso]         = "Traspaso",
            [TipoDocumentoSerie.AjusteInventario] = "Ajuste de Inventario",
        };

    public SerieDocumentoService(ErpDbContext context, ISessionContext session)
    {
        _context = context;
        _session = session;
    }

    public async Task<IReadOnlyList<SerieDocumentoDto>> ListarAsync(CancellationToken ct = default)
    {
        var lista = await _context.SeriesDocumento
            .AsNoTracking()
            .Include(s => s.Sucursal)
            .Where(s => s.EmpresaId == _session.EmpresaId && s.Activo)
            .OrderBy(s => s.TipoDocumento)
            .ThenBy(s => s.SucursalId)
            .ToListAsync(ct);

        return lista.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<SerieDocumentoDto>> ListarTodasAsync(CancellationToken ct = default)
    {
        var lista = await _context.SeriesDocumento
            .AsNoTracking()
            .Include(s => s.Sucursal)
            .Where(s => s.EmpresaId == _session.EmpresaId)
            .OrderBy(s => s.TipoDocumento)
            .ThenBy(s => s.SucursalId)
            .ToListAsync(ct);

        return lista.Select(MapToDto).ToList();
    }

    public async Task<ServiceResult<SerieDocumentoDto>> GuardarAsync(
        GuardarSerieDocumentoDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Prefijo))
            return ServiceResult<SerieDocumentoDto>.Fail("El prefijo es requerido.");

        if (dto.Longitud < 1 || dto.Longitud > 12)
            return ServiceResult<SerieDocumentoDto>.Fail("La longitud debe estar entre 1 y 12 dígitos.");

        if (dto.SiguienteNumero < 1)
            return ServiceResult<SerieDocumentoDto>.Fail("El siguiente número debe ser mayor a 0.");

        var usuarioId = _session.UsuarioId ?? Guid.Empty;
        var prefijo   = dto.Prefijo.Trim().ToUpperInvariant();

        if (dto.Id == 0)
        {
            // Verificar unicidad TipoDocumento+SucursalId por empresa
            var existe = await _context.SeriesDocumento.AnyAsync(
                s => s.EmpresaId == _session.EmpresaId
                  && s.TipoDocumento == dto.TipoDocumento
                  && s.SucursalId == dto.SucursalId, ct);

            if (existe)
                return ServiceResult<SerieDocumentoDto>.Fail(
                    $"Ya existe una serie para '{NombreTipo(dto.TipoDocumento)}' en esta configuración de sucursal.");

            // Verificar prefijo único por empresa
            var prefijoDuplicado = await _context.SeriesDocumento.AnyAsync(
                s => s.EmpresaId == _session.EmpresaId && s.Prefijo == prefijo, ct);

            if (prefijoDuplicado)
                return ServiceResult<SerieDocumentoDto>.Fail($"El prefijo '{prefijo}' ya está en uso.");

            var nueva = new SerieDocumento
            {
                EmpresaId         = _session.EmpresaId,
                SucursalId        = dto.SucursalId,
                TipoDocumento     = dto.TipoDocumento,
                Prefijo           = prefijo,
                Longitud          = dto.Longitud,
                SiguienteNumero   = dto.SiguienteNumero,
                ReinicioAnual     = dto.ReinicioAnual,
                Activo            = dto.Activo,
                FechaCreacion     = DateTime.UtcNow,
                UsuarioCreacionId = usuarioId,
            };

            _context.SeriesDocumento.Add(nueva);
            await _context.SaveChangesAsync(ct);
            await _context.Entry(nueva).Reference(s => s.Sucursal).LoadAsync(ct);
            return ServiceResult<SerieDocumentoDto>.Ok(MapToDto(nueva));
        }
        else
        {
            var serie = await _context.SeriesDocumento
                .Include(s => s.Sucursal)
                .FirstOrDefaultAsync(s => s.Id == dto.Id && s.EmpresaId == _session.EmpresaId, ct);

            if (serie is null)
                return ServiceResult<SerieDocumentoDto>.Fail("Serie no encontrada.", ErrorCode.NotFound);

            // Verificar prefijo único si cambió
            if (!string.Equals(serie.Prefijo, prefijo, StringComparison.Ordinal))
            {
                var prefijoDuplicado = await _context.SeriesDocumento.AnyAsync(
                    s => s.EmpresaId == _session.EmpresaId && s.Prefijo == prefijo && s.Id != dto.Id, ct);
                if (prefijoDuplicado)
                    return ServiceResult<SerieDocumentoDto>.Fail($"El prefijo '{prefijo}' ya está en uso.");
            }

            // No permitir bajar el consecutivo por debajo del último emitido implica que
            // SiguienteNumero solo puede aumentarse, nunca reducirse en una serie activa con documentos.
            // V1: permitimos el cambio manual (admin puede corregir si hay error) con un mínimo de 1.
            serie.Prefijo               = prefijo;
            serie.Longitud              = dto.Longitud;
            serie.SiguienteNumero       = Math.Max(1, dto.SiguienteNumero);
            serie.ReinicioAnual         = dto.ReinicioAnual;
            serie.Activo                = dto.Activo;
            serie.FechaModificacion     = DateTime.UtcNow;
            serie.UsuarioModificacionId = usuarioId;

            await _context.SaveChangesAsync(ct);
            return ServiceResult<SerieDocumentoDto>.Ok(MapToDto(serie));
        }
    }

    public async Task<ServiceResult> EliminarAsync(int id, CancellationToken ct = default)
    {
        var serie = await _context.SeriesDocumento
            .FirstOrDefaultAsync(s => s.Id == id && s.EmpresaId == _session.EmpresaId, ct);

        if (serie is null)
            return ServiceResult.Fail("Serie no encontrada.", ErrorCode.NotFound);

        serie.Borrado               = true;
        serie.Activo                = false;
        serie.FechaModificacion     = DateTime.UtcNow;
        serie.UsuarioModificacionId = _session.UsuarioId ?? Guid.Empty;

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static string NombreTipo(TipoDocumentoSerie t)
        => _nombres.TryGetValue(t, out var n) ? n : t.ToString();

    private static SerieDocumentoDto MapToDto(SerieDocumento s) => new(
        s.Id,
        s.EmpresaId,
        s.SucursalId,
        s.Sucursal?.Nombre,
        s.TipoDocumento,
        _nombres.TryGetValue(s.TipoDocumento, out var n) ? n : s.TipoDocumento.ToString(),
        s.Prefijo,
        s.Longitud,
        s.SiguienteNumero,
        $"{s.Prefijo}-{s.SiguienteNumero.ToString().PadLeft(s.Longitud, '0')}",
        s.ReinicioAnual,
        s.Activo);
}

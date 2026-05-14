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
/// Implementación de <see cref="IParametroGlobalService"/> sobre EF Core.
/// Todos los parámetros se filtran por EmpresaId de sesión. El soft-delete global aplica.
/// </summary>
public sealed class ParametroGlobalService : IParametroGlobalService
{
    private readonly ErpDbContext _context;
    private readonly ISessionContext _session;

    public ParametroGlobalService(ErpDbContext context, ISessionContext session)
    {
        _context = context;
        _session = session;
    }

    public async Task<IReadOnlyList<ParametroGlobalDto>> ListarAsync(CancellationToken ct = default)
    {
        var lista = await _context.ParametrosGlobal
            .AsNoTracking()
            .Where(p => p.EmpresaId == _session.EmpresaId)
            .OrderBy(p => p.Grupo)
            .ThenBy(p => p.OrdenVisual)
            .ThenBy(p => p.Clave)
            .ToListAsync(ct);

        return lista.Select(MapToDto).ToList();
    }

    public async Task<string> ObtenerValorAsync(string clave, string defaultValue = "", CancellationToken ct = default)
    {
        var param = await _context.ParametrosGlobal
            .AsNoTracking()
            .Where(p => p.EmpresaId == _session.EmpresaId && p.Clave == clave && p.Activo)
            .FirstOrDefaultAsync(ct);

        return param?.Valor ?? defaultValue;
    }

    public async Task<decimal> ObtenerDecimalAsync(string clave, decimal defaultValue = 0m, CancellationToken ct = default)
    {
        var valor = await ObtenerValorAsync(clave, ct: ct);
        return decimal.TryParse(valor, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : defaultValue;
    }

    public async Task<int> ObtenerIntAsync(string clave, int defaultValue = 0, CancellationToken ct = default)
    {
        var valor = await ObtenerValorAsync(clave, ct: ct);
        return int.TryParse(valor, out var result) ? result : defaultValue;
    }

    public async Task<bool> ObtenerBoolAsync(string clave, bool defaultValue = false, CancellationToken ct = default)
    {
        var valor = await ObtenerValorAsync(clave, ct: ct);
        return bool.TryParse(valor, out var result) ? result : defaultValue;
    }

    public async Task<ServiceResult<ParametroGlobalDto>> GuardarAsync(
        GuardarParametroGlobalDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Clave))
            return ServiceResult<ParametroGlobalDto>.Fail("La clave del parámetro es requerida.");

        if (string.IsNullOrWhiteSpace(dto.Valor))
            return ServiceResult<ParametroGlobalDto>.Fail("El valor del parámetro es requerido.");

        var usuarioId = _session.UsuarioId ?? Guid.Empty;

        if (dto.Id == 0)
        {
            // Crear — verificar unicidad de clave
            var existe = await _context.ParametrosGlobal.AnyAsync(
                p => p.EmpresaId == _session.EmpresaId && p.Clave == dto.Clave.Trim(), ct);

            if (existe)
                return ServiceResult<ParametroGlobalDto>.Fail($"Ya existe un parámetro con la clave '{dto.Clave}'.");

            var nuevo = new ParametroGlobal
            {
                EmpresaId      = _session.EmpresaId,
                Clave          = dto.Clave.Trim(),
                Valor          = dto.Valor.Trim(),
                Descripcion    = dto.Descripcion?.Trim(),
                TipoDato       = dto.TipoDato,
                Grupo          = string.IsNullOrWhiteSpace(dto.Grupo) ? "General" : dto.Grupo.Trim(),
                OrdenVisual    = dto.OrdenVisual,
                Activo         = dto.Activo,
                FechaCreacion  = DateTime.UtcNow,
                UsuarioCreacionId = usuarioId,
            };

            _context.ParametrosGlobal.Add(nuevo);
            await _context.SaveChangesAsync(ct);
            return ServiceResult<ParametroGlobalDto>.Ok(MapToDto(nuevo));
        }
        else
        {
            // Actualizar
            var param = await _context.ParametrosGlobal
                .FirstOrDefaultAsync(p => p.Id == dto.Id && p.EmpresaId == _session.EmpresaId, ct);

            if (param is null)
                return ServiceResult<ParametroGlobalDto>.Fail("Parámetro no encontrado.", ErrorCode.NotFound);

            // Verificar unicidad si cambió la clave
            if (!string.Equals(param.Clave, dto.Clave.Trim(), StringComparison.Ordinal))
            {
                var claveEnUso = await _context.ParametrosGlobal.AnyAsync(
                    p => p.EmpresaId == _session.EmpresaId && p.Clave == dto.Clave.Trim() && p.Id != dto.Id, ct);
                if (claveEnUso)
                    return ServiceResult<ParametroGlobalDto>.Fail($"Ya existe un parámetro con la clave '{dto.Clave}'.");
            }

            param.Clave               = dto.Clave.Trim();
            param.Valor               = dto.Valor.Trim();
            param.Descripcion         = dto.Descripcion?.Trim();
            param.TipoDato            = dto.TipoDato;
            param.Grupo               = string.IsNullOrWhiteSpace(dto.Grupo) ? "General" : dto.Grupo.Trim();
            param.OrdenVisual         = dto.OrdenVisual;
            param.Activo              = dto.Activo;
            param.FechaModificacion   = DateTime.UtcNow;
            param.UsuarioModificacionId = usuarioId;

            await _context.SaveChangesAsync(ct);
            return ServiceResult<ParametroGlobalDto>.Ok(MapToDto(param));
        }
    }

    public async Task<ServiceResult> EliminarAsync(int id, CancellationToken ct = default)
    {
        var param = await _context.ParametrosGlobal
            .FirstOrDefaultAsync(p => p.Id == id && p.EmpresaId == _session.EmpresaId, ct);

        if (param is null)
            return ServiceResult.Fail("Parámetro no encontrado.", ErrorCode.NotFound);

        param.Borrado               = true;
        param.FechaModificacion     = DateTime.UtcNow;
        param.UsuarioModificacionId = _session.UsuarioId ?? Guid.Empty;

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static ParametroGlobalDto MapToDto(ParametroGlobal p) => new(
        p.Id, p.Clave, p.Valor, p.Descripcion, p.TipoDato, p.Grupo, p.OrdenVisual, p.Activo);
}

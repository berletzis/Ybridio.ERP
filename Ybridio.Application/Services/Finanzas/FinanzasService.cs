using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Finanzas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Domain.Finanzas;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Finanzas;

/// <summary>
/// Servicio de gastos e ingresos operativos con enforcement de autorización runtime.
/// Reutiliza <see cref="IErpAuthorizationService"/> para validar permisos por clave tipada.
/// </summary>
public sealed class FinanzasService : IFinanzasService
{
    private readonly ErpDbContext             _context;
    private readonly IErpAuthorizationService _auth;

    public FinanzasService(ErpDbContext context, IErpAuthorizationService auth)
    {
        _context = context;
        _auth    = auth;
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<IReadOnlyList<MovimientoFinancieroDto>>> ListarAsync(
        int empresaId, TipoMovimientoFinanciero tipo,
        int? sucursalId = null, DateTime? desde = null, DateTime? hasta = null,
        CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Finanzas.Ver, ct))
            return ServiceResult<IReadOnlyList<MovimientoFinancieroDto>>.Fail(
                "Sin permiso para ver finanzas (finanzas.ver).", ErrorCode.Unauthorized);

        var query = _context.MovimientosFinancieros
            .AsNoTracking()
            .Where(m => m.EmpresaId == empresaId && m.Tipo == tipo);

        if (sucursalId.HasValue)
            query = query.Where(m => m.SucursalId == sucursalId);
        if (desde.HasValue)
            query = query.Where(m => m.Fecha >= desde.Value);
        if (hasta.HasValue)
            query = query.Where(m => m.Fecha <= hasta.Value);

        var lista = await query
            .Include(m => m.Categoria)
            .OrderByDescending(m => m.Fecha)
            .ThenByDescending(m => m.Id)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<MovimientoFinancieroDto>>.Ok(
            lista.Select(MapToDto).ToList());
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<MovimientoFinancieroDto>> ObtenerPorIdAsync(
        long id, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Finanzas.Ver, ct))
            return ServiceResult<MovimientoFinancieroDto>.Fail(
                "Sin permiso para ver finanzas (finanzas.ver).", ErrorCode.Unauthorized);

        var m = await _context.MovimientosFinancieros
            .AsNoTracking()
            .Include(m => m.Categoria)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        return m is null
            ? ServiceResult<MovimientoFinancieroDto>.Fail("Movimiento no encontrado.", ErrorCode.NotFound)
            : ServiceResult<MovimientoFinancieroDto>.Ok(MapToDto(m));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<MovimientoFinancieroDto>> CrearAsync(
        CrearMovimientoFinancieroDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Finanzas.Crear, ct))
            return ServiceResult<MovimientoFinancieroDto>.Fail(
                "Sin permiso para crear movimientos financieros (finanzas.crear).", ErrorCode.Unauthorized);

        var movimiento = new MovimientoFinanciero
        {
            EmpresaId         = dto.EmpresaId,
            SucursalId        = dto.SucursalId,
            Tipo              = dto.Tipo,
            Contexto          = dto.Contexto,
            CategoriaId       = dto.CategoriaId,
            Concepto          = dto.Concepto.Trim(),
            Monto             = dto.Monto,
            Fecha             = dto.Fecha,
            Observaciones     = dto.Observaciones?.Trim(),
            FechaCreacion     = DateTime.UtcNow,
            UsuarioCreacionId = usuarioId,
            Borrado           = false
        };

        _context.MovimientosFinancieros.Add(movimiento);
        await _context.SaveChangesAsync(ct);
        return await ObtenerPorIdAsync(movimiento.Id, ct);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<MovimientoFinancieroDto>> ActualizarAsync(
        long id, ActualizarMovimientoFinancieroDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Finanzas.Editar, ct))
            return ServiceResult<MovimientoFinancieroDto>.Fail(
                "Sin permiso para editar movimientos financieros (finanzas.editar).", ErrorCode.Unauthorized);

        var m = await _context.MovimientosFinancieros.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null)
            return ServiceResult<MovimientoFinancieroDto>.Fail("Movimiento no encontrado.", ErrorCode.NotFound);

        m.CategoriaId           = dto.CategoriaId;
        m.Concepto              = dto.Concepto.Trim();
        m.Monto                 = dto.Monto;
        m.Fecha                 = dto.Fecha;
        m.Observaciones         = dto.Observaciones?.Trim();
        m.FechaModificacion     = DateTime.UtcNow;
        m.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return await ObtenerPorIdAsync(id, ct);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarAsync(long id, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Finanzas.Eliminar, ct))
            return ServiceResult.Fail(
                "Sin permiso para eliminar movimientos financieros (finanzas.eliminar).", ErrorCode.Unauthorized);

        var m = await _context.MovimientosFinancieros.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return ServiceResult.Fail("Movimiento no encontrado.", ErrorCode.NotFound);

        m.Borrado               = true;
        m.FechaModificacion     = DateTime.UtcNow;
        m.UsuarioModificacionId = usuarioId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CategoriaFinancieraDto>> ListarCategoriasAsync(
        int empresaId, string? tipoAplicable = null, CancellationToken ct = default)
    {
        var query = _context.CategoriasFinancieras
            .AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Activo);

        if (!string.IsNullOrWhiteSpace(tipoAplicable))
            query = query.Where(c => c.TipoAplicable == tipoAplicable || c.TipoAplicable == "Ambos");

        return await query
            .OrderBy(c => c.Nombre)
            .Select(c => new CategoriaFinancieraDto(c.Id, c.EmpresaId, c.TipoAplicable, c.Nombre, c.Descripcion, c.Color, c.Activo))
            .ToListAsync(ct);
    }

    private static MovimientoFinancieroDto MapToDto(MovimientoFinanciero m) =>
        new(m.Id, m.EmpresaId, m.SucursalId, m.Tipo, m.Contexto,
            m.CategoriaId, m.Categoria?.Nombre, m.Categoria?.Color,
            m.Concepto, m.Monto, m.Fecha, m.Observaciones);
}

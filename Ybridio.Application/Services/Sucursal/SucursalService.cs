using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Core;
using Ybridio.Infrastructure.Persistence;
using DomainSucursal = Ybridio.Domain.Core.Sucursal;

namespace Ybridio.Application.Services.Sucursal;

public sealed class SucursalService : ISucursalService
{
    private readonly ErpDbContext _context;

    public SucursalService(ErpDbContext context) => _context = context;

    public async Task<IReadOnlyList<SucursalDto>> ListarPorEmpresaAsync(
        int empresaId, CancellationToken ct = default)
    {
        var sucursales = await _context.Sucursales
            .AsNoTracking()
            .Where(s => s.EmpresaId == empresaId)
            .OrderBy(s => s.Nombre)
            .ToListAsync(ct);

        return sucursales.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<SucursalDto>> ListarPorUsuarioAsync(
        Guid usuarioId, CancellationToken ct = default)
    {
        var sucursalIds = await _context.UsuariosSucursales
            .AsNoTracking()
            .Where(us => us.UsuarioId == usuarioId && us.SucursalId.HasValue)
            .Select(us => us.SucursalId!.Value)
            .Distinct()
            .ToListAsync(ct);

        if (sucursalIds.Count == 0) return [];

        var sucursales = await _context.Sucursales
            .AsNoTracking()
            .Where(s => sucursalIds.Contains(s.Id))
            .OrderBy(s => s.Nombre)
            .ToListAsync(ct);

        return sucursales.Select(MapToDto).ToList();
    }

    public async Task<ServiceResult<SucursalDto>> CrearAsync(
        int empresaId, string nombre, Guid usuarioId, CancellationToken ct = default)
    {
        var nombre_trim = nombre.Trim();
        if (string.IsNullOrWhiteSpace(nombre_trim))
            return ServiceResult<SucursalDto>.Fail("El nombre de la sucursal es obligatorio.");

        var sucursal = new DomainSucursal
        {
            EmpresaId         = empresaId,
            Nombre            = nombre_trim,
            FechaCreacion     = DateTime.UtcNow,
            UsuarioCreacionId = usuarioId,
            Borrado           = false
        };

        _context.Sucursales.Add(sucursal);
        await _context.SaveChangesAsync(ct);
        return ServiceResult<SucursalDto>.Ok(MapToDto(sucursal));
    }

    public async Task<ServiceResult<SucursalDto>> ActualizarAsync(
        int sucursalId, string nombre, Guid usuarioId, CancellationToken ct = default)
    {
        var sucursal = await _context.Sucursales.FirstOrDefaultAsync(s => s.Id == sucursalId, ct);
        if (sucursal is null)
            return ServiceResult<SucursalDto>.Fail("Sucursal no encontrada.", ErrorCode.NotFound);

        sucursal.Nombre               = nombre.Trim();
        sucursal.FechaModificacion    = DateTime.UtcNow;
        sucursal.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<SucursalDto>.Ok(MapToDto(sucursal));
    }

    public async Task<ServiceResult> EliminarAsync(
        int sucursalId, Guid usuarioId, CancellationToken ct = default)
    {
        var sucursal = await _context.Sucursales.FirstOrDefaultAsync(s => s.Id == sucursalId, ct);
        if (sucursal is null)
            return ServiceResult.Fail("Sucursal no encontrada.", ErrorCode.NotFound);

        sucursal.Borrado               = true;
        sucursal.FechaModificacion     = DateTime.UtcNow;
        sucursal.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static SucursalDto MapToDto(DomainSucursal s) => new(s.Id, s.EmpresaId, s.Nombre);
}

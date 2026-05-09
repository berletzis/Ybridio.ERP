using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Finanzas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Domain.Finanzas;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Finanzas;

/// <summary>
/// Servicio de cuentas por pagar con enforcement de autorización runtime.
/// </summary>
public sealed class CxPService : ICxPService
{
    private readonly ErpDbContext             _context;
    private readonly IErpAuthorizationService _auth;

    public CxPService(ErpDbContext context, IErpAuthorizationService auth)
    {
        _context = context;
        _auth    = auth;
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<IReadOnlyList<CxPDto>>> ListarAsync(
        int empresaId, int? sucursalId = null, bool soloVigentes = false,
        DateTime? desde = null, DateTime? hasta = null, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.CxP.Ver, ct))
            return ServiceResult<IReadOnlyList<CxPDto>>.Fail(
                "Sin permiso para ver cuentas por pagar (cxp.ver).", ErrorCode.Unauthorized);

        var query = _context.CuentasPorPagar
            .AsNoTracking()
            .Where(c => c.EmpresaId == empresaId);

        if (sucursalId.HasValue)
            query = query.Where(c => c.SucursalId == sucursalId);

        if (soloVigentes)
            query = query.Where(c => c.MontoPagado < c.MontoOriginal);

        if (desde.HasValue)
            query = query.Where(c => c.FechaEmision >= desde.Value);
        if (hasta.HasValue)
            query = query.Where(c => c.FechaEmision <= hasta.Value);

        var lista = await query
            .OrderByDescending(c => c.FechaVencimiento)
            .ThenByDescending(c => c.Id)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<CxPDto>>.Ok(lista.Select(MapToDto).ToList());
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<CxPDto>> CrearAsync(
        CrearCxPDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.CxP.Crear, ct))
            return ServiceResult<CxPDto>.Fail(
                "Sin permiso para crear cuentas por pagar (cxp.crear).", ErrorCode.Unauthorized);

        if (dto.MontoOriginal <= 0)
            return ServiceResult<CxPDto>.Fail("El monto original debe ser mayor a cero.", ErrorCode.ValidationFailed);

        var cxp = new CuentaPorPagar
        {
            EmpresaId         = dto.EmpresaId,
            SucursalId        = dto.SucursalId,
            NombreAcreedor    = dto.NombreAcreedor.Trim(),
            Concepto          = dto.Concepto.Trim(),
            MontoOriginal     = dto.MontoOriginal,
            MontoPagado       = 0,
            FechaEmision      = dto.FechaEmision,
            FechaVencimiento  = dto.FechaVencimiento,
            Observaciones     = dto.Observaciones?.Trim(),
            FechaCreacion     = DateTime.UtcNow,
            UsuarioCreacionId = usuarioId,
            Borrado           = false
        };

        _context.CuentasPorPagar.Add(cxp);
        await _context.SaveChangesAsync(ct);
        return ServiceResult<CxPDto>.Ok(MapToDto(cxp));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<CxPDto>> RegistrarPagoAsync(
        RegistrarPagoCxPDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.CxP.Editar, ct))
            return ServiceResult<CxPDto>.Fail(
                "Sin permiso para registrar pagos de CxP (cxp.editar).", ErrorCode.Unauthorized);

        var cxp = await _context.CuentasPorPagar.FirstOrDefaultAsync(c => c.Id == dto.CxPId, ct);
        if (cxp is null) return ServiceResult<CxPDto>.Fail("Cuenta por pagar no encontrada.", ErrorCode.NotFound);

        if (dto.Monto <= 0)
            return ServiceResult<CxPDto>.Fail("El monto del pago debe ser mayor a cero.", ErrorCode.ValidationFailed);

        var saldo = cxp.MontoOriginal - cxp.MontoPagado;
        if (dto.Monto > saldo)
            return ServiceResult<CxPDto>.Fail(
                $"El pago ({dto.Monto:C}) supera el saldo pendiente ({saldo:C}).", ErrorCode.ValidationFailed);

        cxp.MontoPagado             += dto.Monto;
        cxp.FechaModificacion        = DateTime.UtcNow;
        cxp.UsuarioModificacionId    = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<CxPDto>.Ok(MapToDto(cxp));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarAsync(long id, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.CxP.Editar, ct))
            return ServiceResult.Fail("Sin permiso para eliminar CxP (cxp.editar).", ErrorCode.Unauthorized);

        var cxp = await _context.CuentasPorPagar.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cxp is null) return ServiceResult.Fail("Cuenta por pagar no encontrada.", ErrorCode.NotFound);

        cxp.Borrado                  = true;
        cxp.FechaModificacion        = DateTime.UtcNow;
        cxp.UsuarioModificacionId    = usuarioId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static CxPDto MapToDto(CuentaPorPagar c)
    {
        var saldo    = c.MontoOriginal - c.MontoPagado;
        var vencida  = c.FechaVencimiento < DateTime.Today && saldo > 0;
        return new(c.Id, c.EmpresaId, c.SucursalId, c.NombreAcreedor, c.Concepto,
            c.MontoOriginal, c.MontoPagado, saldo, c.FechaEmision, c.FechaVencimiento, vencida, c.Observaciones);
    }
}

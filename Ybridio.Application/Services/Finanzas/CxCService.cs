using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Finanzas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Domain.Finanzas;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Finanzas;

/// <summary>
/// Servicio de cuentas por cobrar con enforcement de autorización runtime.
/// </summary>
public sealed class CxCService : ICxCService
{
    private readonly ErpDbContext             _context;
    private readonly IErpAuthorizationService _auth;

    public CxCService(ErpDbContext context, IErpAuthorizationService auth)
    {
        _context = context;
        _auth    = auth;
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<IReadOnlyList<CxCDto>>> ListarAsync(
        int empresaId, int? sucursalId = null, bool soloVigentes = false,
        DateTime? desde = null, DateTime? hasta = null, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.CxC.Ver, ct))
            return ServiceResult<IReadOnlyList<CxCDto>>.Fail(
                "Sin permiso para ver cuentas por cobrar (cxc.ver).", ErrorCode.Unauthorized);

        var query = _context.CuentasPorCobrar
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

        return ServiceResult<IReadOnlyList<CxCDto>>.Ok(lista.Select(MapToDto).ToList());
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<CxCDto>> CrearAsync(
        CrearCxCDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.CxC.Crear, ct))
            return ServiceResult<CxCDto>.Fail(
                "Sin permiso para crear cuentas por cobrar (cxc.crear).", ErrorCode.Unauthorized);

        if (dto.MontoOriginal <= 0)
            return ServiceResult<CxCDto>.Fail("El monto original debe ser mayor a cero.", ErrorCode.ValidationFailed);

        var cxc = new CuentaPorCobrar
        {
            EmpresaId         = dto.EmpresaId,
            SucursalId        = dto.SucursalId,
            NombreDeudor      = dto.NombreDeudor.Trim(),
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

        _context.CuentasPorCobrar.Add(cxc);
        await _context.SaveChangesAsync(ct);
        return ServiceResult<CxCDto>.Ok(MapToDto(cxc));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<CxCDto>> RegistrarPagoAsync(
        RegistrarPagoCxCDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.CxC.Editar, ct))
            return ServiceResult<CxCDto>.Fail(
                "Sin permiso para registrar pagos de CxC (cxc.editar).", ErrorCode.Unauthorized);

        var cxc = await _context.CuentasPorCobrar.FirstOrDefaultAsync(c => c.Id == dto.CxCId, ct);
        if (cxc is null) return ServiceResult<CxCDto>.Fail("Cuenta por cobrar no encontrada.", ErrorCode.NotFound);

        if (dto.Monto <= 0)
            return ServiceResult<CxCDto>.Fail("El monto del pago debe ser mayor a cero.", ErrorCode.ValidationFailed);

        var saldo = cxc.MontoOriginal - cxc.MontoPagado;
        if (dto.Monto > saldo)
            return ServiceResult<CxCDto>.Fail(
                $"El pago ({dto.Monto:C}) supera el saldo pendiente ({saldo:C}).", ErrorCode.ValidationFailed);

        cxc.MontoPagado             += dto.Monto;
        cxc.FechaModificacion        = DateTime.UtcNow;
        cxc.UsuarioModificacionId    = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<CxCDto>.Ok(MapToDto(cxc));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarAsync(long id, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.CxC.Editar, ct))
            return ServiceResult.Fail("Sin permiso para eliminar CxC (cxc.editar).", ErrorCode.Unauthorized);

        var cxc = await _context.CuentasPorCobrar.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cxc is null) return ServiceResult.Fail("Cuenta por cobrar no encontrada.", ErrorCode.NotFound);

        cxc.Borrado                  = true;
        cxc.FechaModificacion        = DateTime.UtcNow;
        cxc.UsuarioModificacionId    = usuarioId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static CxCDto MapToDto(CuentaPorCobrar c)
    {
        var saldo    = c.MontoOriginal - c.MontoPagado;
        var vencida  = c.FechaVencimiento < DateTime.Today && saldo > 0;
        return new(c.Id, c.EmpresaId, c.SucursalId, c.NombreDeudor, c.Concepto,
            c.MontoOriginal, c.MontoPagado, saldo, c.FechaEmision, c.FechaVencimiento, vencida, c.Observaciones);
    }
}

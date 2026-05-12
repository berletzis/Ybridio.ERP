using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Domain.Ventas;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Venta;

/// <summary>
/// Servicio de gestión de cotizaciones con enforcement de autorización runtime.
/// </summary>
public sealed class CotizacionService : ICotizacionService
{
    private readonly ErpDbContext             _context;
    private readonly IErpAuthorizationService _auth;

    public CotizacionService(ErpDbContext context, IErpAuthorizationService auth)
    {
        _context = context;
        _auth    = auth;
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<IReadOnlyList<CotizacionResumenDto>>> ListarAsync(
        int empresaId, DateTime? desde = null, DateTime? hasta = null, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Cotizacion.Ver, ct))
            return ServiceResult<IReadOnlyList<CotizacionResumenDto>>.Fail(
                "Sin permiso para ver cotizaciones (cotizacion.ver).", ErrorCode.Unauthorized);

        var query = _context.Cotizaciones.AsNoTracking().Where(c => c.EmpresaId == empresaId);
        if (desde.HasValue) query = query.Where(c => c.Fecha >= desde.Value);
        if (hasta.HasValue) query = query.Where(c => c.Fecha <= hasta.Value);

        var lista = await query.OrderByDescending(c => c.Fecha).ThenByDescending(c => c.Id).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<CotizacionResumenDto>>.Ok(lista.Select(MapToResumen).ToList());
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<CotizacionDto>> ObtenerConDetallesAsync(long id, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Cotizacion.Ver, ct))
            return ServiceResult<CotizacionDto>.Fail("Sin permiso (cotizacion.ver).", ErrorCode.Unauthorized);

        var c = await _context.Cotizaciones.AsNoTracking()
            .Include(x => x.Detalles)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return c is null
            ? ServiceResult<CotizacionDto>.Fail("Cotización no encontrada.", ErrorCode.NotFound)
            : ServiceResult<CotizacionDto>.Ok(MapToDto(c));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<CotizacionDto>> CrearAsync(CrearCotizacionDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Cotizacion.Crear, ct))
            return ServiceResult<CotizacionDto>.Fail("Sin permiso para crear cotizaciones (cotizacion.crear).", ErrorCode.Unauthorized);

        if (!dto.Detalles.Any())
            return ServiceResult<CotizacionDto>.Fail("La cotización debe tener al menos un detalle.", ErrorCode.ValidationFailed);

        var detalles = dto.Detalles.Select(d => new CotizacionDetalle
        {
            ProductoId     = d.ProductoId,
            Descripcion    = d.Descripcion.Trim(),
            Cantidad       = d.Cantidad,
            PrecioUnitario = d.PrecioUnitario,
            Importe        = d.Cantidad * d.PrecioUnitario  // Importe = Cantidad × PrecioUnitario
        }).ToList();

        var subtotal = detalles.Sum(d => d.Importe);

        var cotizacion = new Cotizacion
        {
            EmpresaId              = dto.EmpresaId,
            SucursalId             = dto.SucursalId,
            RelacionComercialId    = dto.RelacionComercialId,
            NombreCliente          = dto.NombreCliente.Trim(),
            Estatus           = EstatusCotizacion.Borrador,
            Fecha             = dto.Fecha,
            FechaVigencia     = dto.FechaVigencia,
            Subtotal          = subtotal,
            Total             = subtotal,  // V1: Total = Subtotal (sin IVA independiente)
            Observaciones     = dto.Observaciones?.Trim(),
            FechaCreacion     = DateTime.UtcNow,
            UsuarioCreacionId = usuarioId,
            Borrado           = false,
            Detalles          = detalles
        };

        _context.Cotizaciones.Add(cotizacion);
        await _context.SaveChangesAsync(ct);
        return ServiceResult<CotizacionDto>.Ok(MapToDto(cotizacion));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> CambiarEstatusAsync(long id, EstatusCotizacion nuevoEstatus, Guid usuarioId, CancellationToken ct = default)
    {
        var permiso = nuevoEstatus == EstatusCotizacion.Cancelada
            ? PermisosClave.Cotizacion.Cancelar
            : PermisosClave.Cotizacion.Editar;

        if (!await _auth.PuedeAsync(permiso, ct))
            return ServiceResult.Fail($"Sin permiso para esta operación ({permiso}).", ErrorCode.Unauthorized);

        var c = await _context.Cotizaciones.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return ServiceResult.Fail("Cotización no encontrada.", ErrorCode.NotFound);
        if (c.Estatus == EstatusCotizacion.Cancelada)
            return ServiceResult.Fail("No se puede modificar una cotización cancelada.", ErrorCode.ValidationFailed);

        c.Estatus               = nuevoEstatus;
        c.FechaModificacion     = DateTime.UtcNow;
        c.UsuarioModificacionId = usuarioId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarAsync(long id, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Cotizacion.Cancelar, ct))
            return ServiceResult.Fail("Sin permiso (cotizacion.cancelar).", ErrorCode.Unauthorized);

        var c = await _context.Cotizaciones.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return ServiceResult.Fail("Cotización no encontrada.", ErrorCode.NotFound);

        c.Borrado = true; c.FechaModificacion = DateTime.UtcNow; c.UsuarioModificacionId = usuarioId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static string EstatusTexto(EstatusCotizacion e) => e switch
    {
        EstatusCotizacion.Borrador  => "Borrador",
        EstatusCotizacion.Enviada   => "Enviada",
        EstatusCotizacion.Aprobada  => "Aprobada",
        EstatusCotizacion.Cancelada => "Cancelada",
        _                           => e.ToString()
    };

    private static CotizacionResumenDto MapToResumen(Cotizacion c) =>
        new(c.Id, c.EmpresaId, c.NombreCliente, c.Estatus, EstatusTexto(c.Estatus),
            c.Fecha, c.FechaVigencia, c.Total, c.Observaciones);

    // ── Document workflow ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<ServiceResult<CotizacionDto>> ActualizarAsync(
        long id, ActualizarCotizacionDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Cotizacion.Editar, ct))
            return ServiceResult<CotizacionDto>.Fail("Sin permiso (cotizacion.editar).", ErrorCode.Unauthorized);

        var c = await _context.Cotizaciones.Include(x => x.Detalles)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return ServiceResult<CotizacionDto>.Fail("Cotización no encontrada.", ErrorCode.NotFound);
        if (c.Estatus == EstatusCotizacion.Cancelada)
            return ServiceResult<CotizacionDto>.Fail("No se puede editar una cotización cancelada.", ErrorCode.ValidationFailed);

        c.RelacionComercialId   = dto.RelacionComercialId;
        c.NombreCliente         = dto.NombreCliente.Trim();
        c.Fecha                 = dto.Fecha;
        c.FechaVigencia         = dto.FechaVigencia;
        c.Observaciones         = dto.Observaciones?.Trim();
        c.FechaModificacion     = DateTime.UtcNow;
        c.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<CotizacionDto>.Ok(MapToDto(c));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<DetalleLineaDto>> AgregarDetalleAsync(
        long cotizacionId, CrearDetalleLineaDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Cotizacion.Editar, ct))
            return ServiceResult<DetalleLineaDto>.Fail("Sin permiso (cotizacion.editar).", ErrorCode.Unauthorized);

        var c = await _context.Cotizaciones.Include(x => x.Detalles)
            .FirstOrDefaultAsync(x => x.Id == cotizacionId, ct);
        if (c is null) return ServiceResult<DetalleLineaDto>.Fail("Cotización no encontrada.", ErrorCode.NotFound);

        var detalle = new CotizacionDetalle
        {
            CotizacionId   = cotizacionId,
            ProductoId     = dto.ProductoId,
            Descripcion    = dto.Descripcion.Trim(),
            Cantidad       = dto.Cantidad,
            PrecioUnitario = dto.PrecioUnitario,
            Importe        = dto.Cantidad * dto.PrecioUnitario  // Importe = Cantidad × PrecioUnitario
        };
        c.Detalles.Add(detalle);

        // Recalcular: Subtotal = SUM(detalles.Importe); Total = Subtotal (V1 sin IVA independiente)
        c.Subtotal              = c.Detalles.Sum(d => d.Importe);
        c.Total                 = c.Subtotal;
        c.FechaModificacion     = DateTime.UtcNow;
        c.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<DetalleLineaDto>.Ok(
            new(detalle.Id, detalle.ProductoId, detalle.Descripcion, detalle.Cantidad, detalle.PrecioUnitario, detalle.Importe));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarDetalleAsync(long detalleId, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Cotizacion.Editar, ct))
            return ServiceResult.Fail("Sin permiso (cotizacion.editar).", ErrorCode.Unauthorized);

        var d = await _context.CotizacionesDetalle
            .Include(x => x.Cotizacion).ThenInclude(c => c.Detalles)
            .FirstOrDefaultAsync(x => x.Id == detalleId, ct);
        if (d is null) return ServiceResult.Fail("Detalle no encontrado.", ErrorCode.NotFound);

        _context.CotizacionesDetalle.Remove(d);

        // Recalcular totales excluyendo el detalle eliminado
        d.Cotizacion.Subtotal              = d.Cotizacion.Detalles.Where(x => x.Id != detalleId).Sum(x => x.Importe);
        d.Cotizacion.Total                 = d.Cotizacion.Subtotal;
        d.Cotizacion.FechaModificacion     = DateTime.UtcNow;
        d.Cotizacion.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<PedidoDto>> ConvertirAPedidoAsync(long cotizacionId, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Pedido.Crear, ct))
            return ServiceResult<PedidoDto>.Fail("Sin permiso para crear pedidos (pedido.crear).", ErrorCode.Unauthorized);

        var c = await _context.Cotizaciones.Include(x => x.Detalles)
            .FirstOrDefaultAsync(x => x.Id == cotizacionId, ct);
        if (c is null) return ServiceResult<PedidoDto>.Fail("Cotización no encontrada.", ErrorCode.NotFound);
        if (c.Estatus != EstatusCotizacion.Aprobada)
            return ServiceResult<PedidoDto>.Fail("Solo se pueden convertir cotizaciones en estado Aprobada.", ErrorCode.ValidationFailed);

        var detalles = c.Detalles.Select(d => new PedidoDetalle
        {
            ProductoId     = d.ProductoId,
            Descripcion    = d.Descripcion,
            Cantidad       = d.Cantidad,
            PrecioUnitario = d.PrecioUnitario,
            Importe        = d.Importe
        }).ToList();

        var pedido = new Pedido
        {
            EmpresaId              = c.EmpresaId,
            SucursalId             = c.SucursalId,
            RelacionComercialId    = c.RelacionComercialId,
            NombreCliente          = c.NombreCliente,
            CotizacionId      = c.Id,
            Estatus           = EstatusPedido.Nuevo,
            Fecha             = DateTime.Today,
            Total             = c.Total,
            Observaciones     = $"Generado desde Cotización #{c.Id}",
            FechaCreacion     = DateTime.UtcNow,
            UsuarioCreacionId = usuarioId,
            Borrado           = false,
            Detalles          = detalles
        };

        _context.Pedidos.Add(pedido);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<PedidoDto>.Ok(new(pedido.Id, pedido.EmpresaId, pedido.SucursalId, pedido.RelacionComercialId,
            pedido.NombreCliente, pedido.CotizacionId, pedido.Estatus, "Nuevo", pedido.Fecha,
            pedido.FechaEntregaCompromiso, pedido.Total, pedido.Observaciones,
            detalles.Select(d => new DetalleLineaDto(d.Id, d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario, d.Importe)).ToList()));
    }

    private static CotizacionDto MapToDto(Cotizacion c) =>
        new(c.Id, c.EmpresaId, c.SucursalId, c.RelacionComercialId, c.NombreCliente,
            c.Estatus, EstatusTexto(c.Estatus), c.Fecha, c.FechaVigencia,
            c.Subtotal, c.Total, c.Observaciones,
            c.Detalles.Select(d => new DetalleLineaDto(d.Id, d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario, d.Importe)).ToList());
}

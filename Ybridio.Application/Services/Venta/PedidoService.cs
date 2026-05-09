using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Domain.Ventas;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Venta;

/// <summary>
/// Servicio de gestión de pedidos con enforcement de autorización runtime.
/// </summary>
public sealed class PedidoService : IPedidoService
{
    private readonly ErpDbContext             _context;
    private readonly IErpAuthorizationService _auth;

    public PedidoService(ErpDbContext context, IErpAuthorizationService auth)
    {
        _context = context;
        _auth    = auth;
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<IReadOnlyList<PedidoResumenDto>>> ListarAsync(
        int empresaId, DateTime? desde = null, DateTime? hasta = null, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Pedido.Ver, ct))
            return ServiceResult<IReadOnlyList<PedidoResumenDto>>.Fail(
                "Sin permiso para ver pedidos (pedido.ver).", ErrorCode.Unauthorized);

        var query = _context.Pedidos.AsNoTracking().Where(p => p.EmpresaId == empresaId);
        if (desde.HasValue) query = query.Where(p => p.Fecha >= desde.Value);
        if (hasta.HasValue) query = query.Where(p => p.Fecha <= hasta.Value);

        var lista = await query.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<PedidoResumenDto>>.Ok(lista.Select(MapToResumen).ToList());
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<PedidoDto>> ObtenerConDetallesAsync(long id, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Pedido.Ver, ct))
            return ServiceResult<PedidoDto>.Fail("Sin permiso (pedido.ver).", ErrorCode.Unauthorized);

        var p = await _context.Pedidos.AsNoTracking()
            .Include(x => x.Detalles)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return p is null
            ? ServiceResult<PedidoDto>.Fail("Pedido no encontrado.", ErrorCode.NotFound)
            : ServiceResult<PedidoDto>.Ok(MapToDto(p));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<PedidoDto>> CrearAsync(CrearPedidoDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Pedido.Crear, ct))
            return ServiceResult<PedidoDto>.Fail("Sin permiso para crear pedidos (pedido.crear).", ErrorCode.Unauthorized);

        var detalles = dto.Detalles.Select(d => new PedidoDetalle
        {
            ProductoId     = d.ProductoId,
            Descripcion    = d.Descripcion.Trim(),
            Cantidad       = d.Cantidad,
            PrecioUnitario = d.PrecioUnitario,
            Importe        = d.Cantidad * d.PrecioUnitario  // Importe = Cantidad × PrecioUnitario
        }).ToList();

        var pedido = new Pedido
        {
            EmpresaId              = dto.EmpresaId,
            SucursalId             = dto.SucursalId,
            ClienteId              = dto.ClienteId,
            NombreCliente          = dto.NombreCliente.Trim(),
            CotizacionId           = dto.CotizacionId,
            Estatus                = EstatusPedido.Nuevo,
            Fecha                  = dto.Fecha,
            FechaEntregaCompromiso = dto.FechaEntregaCompromiso,
            Total                  = detalles.Sum(d => d.Importe),
            Observaciones          = dto.Observaciones?.Trim(),
            FechaCreacion          = DateTime.UtcNow,
            UsuarioCreacionId      = usuarioId,
            Borrado                = false,
            Detalles               = detalles
        };

        _context.Pedidos.Add(pedido);
        await _context.SaveChangesAsync(ct);
        return ServiceResult<PedidoDto>.Ok(MapToDto(pedido));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> CambiarEstatusAsync(long id, EstatusPedido nuevoEstatus, Guid usuarioId, CancellationToken ct = default)
    {
        var permiso = nuevoEstatus == EstatusPedido.Cancelado
            ? PermisosClave.Pedido.Cancelar
            : PermisosClave.Pedido.Editar;

        if (!await _auth.PuedeAsync(permiso, ct))
            return ServiceResult.Fail($"Sin permiso ({permiso}).", ErrorCode.Unauthorized);

        var p = await _context.Pedidos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return ServiceResult.Fail("Pedido no encontrado.", ErrorCode.NotFound);
        if (p.Estatus == EstatusPedido.Cancelado)
            return ServiceResult.Fail("No se puede modificar un pedido cancelado.", ErrorCode.ValidationFailed);

        p.Estatus = nuevoEstatus; p.FechaModificacion = DateTime.UtcNow; p.UsuarioModificacionId = usuarioId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarAsync(long id, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Pedido.Cancelar, ct))
            return ServiceResult.Fail("Sin permiso (pedido.cancelar).", ErrorCode.Unauthorized);

        var p = await _context.Pedidos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return ServiceResult.Fail("Pedido no encontrado.", ErrorCode.NotFound);

        p.Borrado = true; p.FechaModificacion = DateTime.UtcNow; p.UsuarioModificacionId = usuarioId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // ── Document workflow ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<ServiceResult<PedidoDto>> ActualizarAsync(
        long id, ActualizarPedidoDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Pedido.Editar, ct))
            return ServiceResult<PedidoDto>.Fail("Sin permiso (pedido.editar).", ErrorCode.Unauthorized);

        var p = await _context.Pedidos.Include(x => x.Detalles).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return ServiceResult<PedidoDto>.Fail("Pedido no encontrado.", ErrorCode.NotFound);
        if (p.Estatus == EstatusPedido.Cancelado)
            return ServiceResult<PedidoDto>.Fail("No se puede editar un pedido cancelado.", ErrorCode.ValidationFailed);

        p.ClienteId              = dto.ClienteId;
        p.NombreCliente          = dto.NombreCliente.Trim();
        p.Fecha                  = dto.Fecha;
        p.FechaEntregaCompromiso = dto.FechaEntregaCompromiso;
        p.Observaciones          = dto.Observaciones?.Trim();
        p.FechaModificacion      = DateTime.UtcNow;
        p.UsuarioModificacionId  = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<PedidoDto>.Ok(MapToDto(p));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<DetalleLineaDto>> AgregarDetalleAsync(
        long pedidoId, CrearDetalleLineaDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Pedido.Editar, ct))
            return ServiceResult<DetalleLineaDto>.Fail("Sin permiso (pedido.editar).", ErrorCode.Unauthorized);

        var p = await _context.Pedidos.Include(x => x.Detalles).FirstOrDefaultAsync(x => x.Id == pedidoId, ct);
        if (p is null) return ServiceResult<DetalleLineaDto>.Fail("Pedido no encontrado.", ErrorCode.NotFound);

        var detalle = new PedidoDetalle
        {
            PedidoId       = pedidoId,
            ProductoId     = dto.ProductoId,
            Descripcion    = dto.Descripcion.Trim(),
            Cantidad       = dto.Cantidad,
            PrecioUnitario = dto.PrecioUnitario,
            Importe        = dto.Cantidad * dto.PrecioUnitario  // Importe = Cantidad × PrecioUnitario
        };
        p.Detalles.Add(detalle);
        p.Total                 = p.Detalles.Sum(d => d.Importe);
        p.FechaModificacion     = DateTime.UtcNow;
        p.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<DetalleLineaDto>.Ok(
            new(detalle.Id, detalle.ProductoId, detalle.Descripcion, detalle.Cantidad, detalle.PrecioUnitario, detalle.Importe));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarDetalleAsync(long detalleId, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Pedido.Editar, ct))
            return ServiceResult.Fail("Sin permiso (pedido.editar).", ErrorCode.Unauthorized);

        var d = await _context.PedidosDetalle
            .Include(x => x.Pedido).ThenInclude(p => p.Detalles)
            .FirstOrDefaultAsync(x => x.Id == detalleId, ct);
        if (d is null) return ServiceResult.Fail("Detalle no encontrado.", ErrorCode.NotFound);

        _context.PedidosDetalle.Remove(d);
        d.Pedido.Total                 = d.Pedido.Detalles.Where(x => x.Id != detalleId).Sum(x => x.Importe);
        d.Pedido.FechaModificacion     = DateTime.UtcNow;
        d.Pedido.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<OrdenTrabajoDto>> GenerarOrdenTrabajoAsync(
        long pedidoId, string descripcionTrabajo, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.OrdenTrabajo.Crear, ct))
            return ServiceResult<OrdenTrabajoDto>.Fail("Sin permiso para crear OT (ordentrabajo.crear).", ErrorCode.Unauthorized);

        var p = await _context.Pedidos.FirstOrDefaultAsync(x => x.Id == pedidoId, ct);
        if (p is null) return ServiceResult<OrdenTrabajoDto>.Fail("Pedido no encontrado.", ErrorCode.NotFound);

        var ot = new OrdenTrabajo
        {
            EmpresaId         = p.EmpresaId,
            SucursalId        = p.SucursalId,
            ClienteId         = p.ClienteId,
            NombreCliente     = p.NombreCliente,
            PedidoId          = p.Id,
            Estatus           = EstatusOrdenTrabajo.Nueva,
            Fecha             = DateTime.Today,
            Descripcion       = descripcionTrabajo.Trim(),
            Observaciones     = $"Generada desde Pedido #{p.Id}",
            Total             = 0,
            FechaCreacion     = DateTime.UtcNow,
            UsuarioCreacionId = usuarioId,
            Borrado           = false
        };

        _context.OrdenesTrabajo.Add(ot);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<OrdenTrabajoDto>.Ok(
            new(ot.Id, ot.EmpresaId, ot.SucursalId, ot.ClienteId, ot.NombreCliente, ot.PedidoId,
                ot.Estatus, "Nueva", ot.Fecha, ot.FechaCompromiso, ot.Descripcion, ot.Observaciones,
                ot.ResponsableId, ot.Total, []));
    }

    private static string EstatusTexto(EstatusPedido e) => e switch
    {
        EstatusPedido.Nuevo      => "Nuevo",
        EstatusPedido.Confirmado => "Confirmado",
        EstatusPedido.EnProceso  => "En Proceso",
        EstatusPedido.Completado => "Completado",
        EstatusPedido.Cancelado  => "Cancelado",
        _                        => e.ToString()
    };

    private static PedidoResumenDto MapToResumen(Pedido p) =>
        new(p.Id, p.EmpresaId, p.NombreCliente, p.CotizacionId,
            p.Estatus, EstatusTexto(p.Estatus), p.Fecha, p.FechaEntregaCompromiso, p.Total, p.Observaciones);

    private static PedidoDto MapToDto(Pedido p) =>
        new(p.Id, p.EmpresaId, p.SucursalId, p.ClienteId, p.NombreCliente, p.CotizacionId,
            p.Estatus, EstatusTexto(p.Estatus), p.Fecha, p.FechaEntregaCompromiso, p.Total, p.Observaciones,
            p.Detalles.Select(d => new DetalleLineaDto(d.Id, d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario, d.Importe)).ToList());
}

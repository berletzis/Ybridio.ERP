using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Application.Services.Folios;
using Ybridio.Domain.Catalogos;
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
    private readonly IFolioGeneratorService   _folioGenerator;

    public PedidoService(
        ErpDbContext             context,
        IErpAuthorizationService auth,
        IFolioGeneratorService   folioGenerator)
    {
        _context        = context;
        _auth           = auth;
        _folioGenerator = folioGenerator;
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<IReadOnlyList<PedidoResumenDto>>> ListarAsync(
        int empresaId, DateTime? desde = null, DateTime? hasta = null, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Pedido.Ver, ct))
            return ServiceResult<IReadOnlyList<PedidoResumenDto>>.Fail(
                "Sin permiso para ver pedidos (pedido.ver).", ErrorCode.Unauthorized);

        var query = _context.Pedidos.AsNoTracking()
            .Where(p => p.EmpresaId == empresaId);
        if (desde.HasValue) query = query.Where(p => p.Fecha >= desde.Value);
        if (hasta.HasValue) query = query.Where(p => p.Fecha <= hasta.Value);

        // Proyección con folio de cotización origen — LEFT JOIN sin cargar entidad completa
        var lista = await query
            .OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id)
            .Select(p => new
            {
                Pedido           = p,
                FolioCotizacion  = p.CotizacionId != null
                    ? _context.Cotizaciones.Where(c => c.Id == p.CotizacionId).Select(c => c.Folio).FirstOrDefault()
                    : null
            })
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<PedidoResumenDto>>.Ok(
            lista.Select(x => MapToResumen(x.Pedido, x.FolioCotizacion)).ToList());
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<PedidoDto>> ObtenerConDetallesAsync(long id, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Pedido.Ver, ct))
            return ServiceResult<PedidoDto>.Fail("Sin permiso (pedido.ver).", ErrorCode.Unauthorized);

        var p = await _context.Pedidos.AsNoTracking()
            .Include(x => x.Detalles).ThenInclude(d => d.Producto)
            .Include(x => x.Cargos)
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
            DescuentoPct   = d.DescuentoPct,
            IvaAplicable   = d.IvaAplicable,
            // CommercialDocumentCalculator — Single Source of Truth (ADR-042)
            Importe        = CommercialDocumentCalculator.CalcularImporteLinea(d.Cantidad, d.PrecioUnitario, d.DescuentoPct)
        }).ToList();

        // Generar folio documental (Document Identity Rule: folio propio por tipo de documento)
        var folio = await _folioGenerator.GenerarFolioAsync(
            dto.EmpresaId, TipoDocumentoSerie.Pedido, dto.SucursalId, ct);

        var pedido = new Pedido
        {
            EmpresaId              = dto.EmpresaId,
            SucursalId             = dto.SucursalId,
            RelacionComercialId    = dto.RelacionComercialId,
            NombreCliente          = dto.NombreCliente.Trim(),
            CotizacionId           = dto.CotizacionId,
            Folio                  = folio,
            Estatus                = EstatusPedido.Borrador,
            Fecha                  = dto.Fecha,
            FechaEntregaCompromiso = dto.FechaEntregaCompromiso,
            Subtotal               = detalles.Sum(d => d.Importe),
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
        if (p.Estatus is EstatusPedido.Cancelado or EstatusPedido.Finalizado)
            return ServiceResult.Fail("No se puede modificar un pedido Finalizado o Cancelado.", ErrorCode.ValidationFailed);

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
        if (p.Estatus is EstatusPedido.Cancelado or EstatusPedido.Finalizado)
            return ServiceResult<PedidoDto>.Fail("No se puede editar un pedido Finalizado o Cancelado.", ErrorCode.ValidationFailed);

        p.RelacionComercialId              = dto.RelacionComercialId;
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
            DescuentoPct   = dto.DescuentoPct,
            IvaAplicable   = dto.IvaAplicable,
            // CommercialDocumentCalculator — Single Source of Truth (ADR-042)
            Importe        = CommercialDocumentCalculator.CalcularImporteLinea(dto.Cantidad, dto.PrecioUnitario, dto.DescuentoPct)
        };
        p.Detalles.Add(detalle);
        // Total = SUM(detalles neto) — sin recalcular cargos aquí (se cargan aparte)
        p.Total                 = p.Detalles.Sum(d => d.Importe);
        p.FechaModificacion     = DateTime.UtcNow;
        p.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<DetalleLineaDto>.Ok(
            new(detalle.Id, detalle.ProductoId, detalle.Descripcion, detalle.Cantidad,
                detalle.PrecioUnitario, detalle.Importe, detalle.DescuentoPct,
                IvaAplicable: detalle.IvaAplicable));
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
            RelacionComercialId         = p.RelacionComercialId,
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
            new(ot.Id, ot.EmpresaId, ot.SucursalId, ot.RelacionComercialId, ot.NombreCliente, ot.PedidoId,
                ot.Estatus, "Nueva", ot.Fecha, ot.FechaCompromiso, ot.Descripcion, ot.Observaciones,
                ot.ResponsableId, ot.Total, []));
    }

    // ── Cargos — Commercial Charges Pattern ──────────────────────────────────

    /// <inheritdoc/>
    public async Task<ServiceResult<PedidoCargoDto>> AgregarCargoAsync(
        long pedidoId, CrearPedidoCargoDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Pedido.Editar, ct))
            return ServiceResult<PedidoCargoDto>.Fail("Sin permiso (pedido.editar).", ErrorCode.Unauthorized);

        if (string.IsNullOrWhiteSpace(dto.Descripcion))
            return ServiceResult<PedidoCargoDto>.Fail("La descripción del cargo es requerida.", ErrorCode.ValidationFailed);

        var p = await _context.Pedidos
            .Include(x => x.Detalles)
            .Include(x => x.Cargos)
            .FirstOrDefaultAsync(x => x.Id == pedidoId, ct);
        if (p is null) return ServiceResult<PedidoCargoDto>.Fail("Pedido no encontrado.", ErrorCode.NotFound);
        if (p.Estatus is EstatusPedido.Finalizado or EstatusPedido.Cancelado)
            return ServiceResult<PedidoCargoDto>.Fail("No se puede agregar cargos a un pedido Finalizado o Cancelado.", ErrorCode.ValidationFailed);

        var cargo = new Ybridio.Domain.Ventas.PedidoCargo
        {
            PedidoId    = pedidoId,
            Descripcion = dto.Descripcion.Trim(),
            Importe     = dto.Importe,
            AplicaIva   = dto.AplicaIva,
            Orden       = dto.Orden
        };
        p.Cargos.Add(cargo);

        // Recalcular Total = SUM(detalles) + SUM(cargos)
        p.Total = p.Detalles.Sum(d => d.Importe) + p.Cargos.Sum(c => c.Importe);
        p.FechaModificacion     = DateTime.UtcNow;
        p.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<PedidoCargoDto>.Ok(
            new(cargo.Id, cargo.Descripcion, cargo.Importe, cargo.AplicaIva, cargo.Orden));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarCargoAsync(long cargoId, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Pedido.Editar, ct))
            return ServiceResult.Fail("Sin permiso (pedido.editar).", ErrorCode.Unauthorized);

        var c = await _context.PedidosCargos
            .Include(x => x.Pedido).ThenInclude(p => p.Detalles)
            .Include(x => x.Pedido).ThenInclude(p => p.Cargos)
            .FirstOrDefaultAsync(x => x.Id == cargoId, ct);
        if (c is null) return ServiceResult.Fail("Cargo no encontrado.", ErrorCode.NotFound);

        _context.PedidosCargos.Remove(c);
        c.Pedido.Total = c.Pedido.Detalles.Sum(d => d.Importe)
                       + c.Pedido.Cargos.Where(x => x.Id != cargoId).Sum(x => x.Importe);
        c.Pedido.FechaModificacion     = DateTime.UtcNow;
        c.Pedido.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static string EstatusTexto(EstatusPedido e) => e switch
    {
        EstatusPedido.Borrador   => "Borrador",
        EstatusPedido.Autorizado => "Autorizado",
        EstatusPedido.EnProceso  => "En Proceso",
        EstatusPedido.Parcial    => "Parcial",
        EstatusPedido.Finalizado => "Finalizado",
        EstatusPedido.Cancelado  => "Cancelado",
        _                        => e.ToString()
    };

    private static PedidoResumenDto MapToResumen(Pedido p, string? folioCotizacion = null) =>
        new(p.Id, p.EmpresaId, p.NombreCliente, p.CotizacionId,
            p.Estatus, EstatusTexto(p.Estatus), p.Fecha, p.FechaEntregaCompromiso, p.Total, p.Observaciones,
            Folio: p.Folio, FolioCotizacionOrigen: folioCotizacion);

    private static PedidoDto MapToDto(Pedido p) =>
        new(p.Id, p.EmpresaId, p.SucursalId, p.RelacionComercialId, p.NombreCliente, p.CotizacionId,
            p.Estatus, EstatusTexto(p.Estatus), p.Fecha, p.FechaEntregaCompromiso, p.Total, p.Observaciones,
            p.Detalles.Select(d => new DetalleLineaDto(
                d.Id, d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario,
                d.Importe, d.DescuentoPct,
                Sku:          d.Producto?.Codigo,
                IvaAplicable: d.IvaAplicable)).ToList(),
            Folio:    p.Folio,
            Cargos:   p.Cargos.OrderBy(c => c.Orden)
                        .Select(c => new PedidoCargoDto(c.Id, c.Descripcion, c.Importe, c.AplicaIva, c.Orden))
                        .ToList(),
            Subtotal: p.Subtotal);
}

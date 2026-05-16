using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Application.Services.Finanzas;
using Ybridio.Application.Services.Folios;
using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Ventas;
using Ybridio.Infrastructure.Persistence;
using DomainVenta       = Ybridio.Domain.Ventas.Venta;
using DomainVentaDet    = Ybridio.Domain.Ventas.VentaDetalle;
using DomainPagoVenta   = Ybridio.Domain.Ventas.PagoVenta;

namespace Ybridio.Application.Services.Venta;

/// <summary>
/// Orquesta el flujo de venta documental PYME:
/// Borrador → Confirmar (descuenta inventario, genera CxC si Crédito) → RegistrarPago → Cancelar.
/// </summary>
public sealed class VentaDocumentalService : IVentaDocumentalService
{
    private readonly ErpDbContext             _context;
    private readonly IErpAuthorizationService _auth;
    private readonly ICxCService              _cxcService;
    private readonly IFolioGeneratorService   _folioGenerator;
    private readonly ILogger<VentaDocumentalService> _logger;

    public VentaDocumentalService(
        ErpDbContext             context,
        IErpAuthorizationService auth,
        ICxCService              cxcService,
        IFolioGeneratorService   folioGenerator,
        ILogger<VentaDocumentalService> logger)
    {
        _context        = context;
        _auth           = auth;
        _cxcService     = cxcService;
        _folioGenerator = folioGenerator;
        _logger         = logger;
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<IReadOnlyList<VentaDocumentalResumenDto>>> ListarAsync(
        int empresaId, DateTime? desde = null, DateTime? hasta = null, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ServiceResult<IReadOnlyList<VentaDocumentalResumenDto>>.Ok(
                Array.Empty<VentaDocumentalResumenDto>());

        // ADR-026 — Strategy A: authorization check uses CancellationToken.None.
        // Authorization parcialmente cancelada dejaría la UI en estado ambiguo.
        if (!await _auth.PuedeAsync(PermisosClave.Venta.Ver, CancellationToken.None))
            return ServiceResult<IReadOnlyList<VentaDocumentalResumenDto>>.Fail(
                "Sin permiso (venta.ver).", ErrorCode.Unauthorized);

        // ADR-026: Re-check after async auth call; ct may have been cancelled during it.
        if (ct.IsCancellationRequested)
            return ServiceResult<IReadOnlyList<VentaDocumentalResumenDto>>.Ok(
                Array.Empty<VentaDocumentalResumenDto>());

        var query = _context.Ventas.AsNoTracking()
            .Where(v => v.EmpresaId == empresaId && v.NombreCliente != null); // solo documentales

        if (desde.HasValue) query = query.Where(v => v.Fecha >= desde.Value);
        if (hasta.HasValue) query = query.Where(v => v.Fecha <= hasta.Value);

        var lista = await query.OrderByDescending(v => v.Fecha).ThenByDescending(v => v.Id).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<VentaDocumentalResumenDto>>.Ok(lista.Select(MapToResumen).ToList());
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<VentaDocumentalDto>> ObtenerConDetallesAsync(long ventaId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Venta.Ver, ct))
            return ServiceResult<VentaDocumentalDto>.Fail("Sin permiso (venta.ver).", ErrorCode.Unauthorized);

        var v = await _context.Ventas.AsNoTracking()
            .Include(x => x.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(x => x.Id == ventaId, ct);

        if (v is null)
            return ServiceResult<VentaDocumentalDto>.Fail("Venta no encontrada.", ErrorCode.NotFound);

        var pagos = await _context.Set<DomainPagoVenta>().AsNoTracking()
            .Where(p => p.VentaId == ventaId).OrderBy(p => p.Fecha).ToListAsync(ct);

        return ServiceResult<VentaDocumentalDto>.Ok(MapToDto(v, pagos));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<VentaDocumentalDto>> CrearAsync(
        CrearVentaDocumentalDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (!await _auth.PuedeAsync(PermisosClave.Venta.Crear, ct))
            return ServiceResult<VentaDocumentalDto>.Fail("Sin permiso (venta.crear).", ErrorCode.Unauthorized);

        if (dto.Detalles.Count == 0)
            return ServiceResult<VentaDocumentalDto>.Fail(
                "La venta debe tener al menos un detalle.", ErrorCode.VentaNoDetalles);

        if (dto.Detalles.Any(d => d.Cantidad <= 0 || d.PrecioUnitario <= 0))
            return ServiceResult<VentaDocumentalDto>.Fail(
                "Cantidad y precio deben ser mayores a cero.", ErrorCode.ValidationFailed);

        var detalles = dto.Detalles.Select(d => new DomainVentaDet
        {
            ProductoId = d.ProductoId ?? 0,
            Cantidad   = d.Cantidad,
            Precio     = d.PrecioUnitario,
            // Importe = Cantidad × PrecioUnitario
            Importe    = d.Cantidad * d.PrecioUnitario,
        }).ToList();

        var total = detalles.Sum(d => d.Importe ?? 0m);

        // Generar folio documental (Document Identity Rule: folio propio por tipo de documento)
        var folio = await _folioGenerator.GenerarFolioAsync(
            dto.EmpresaId, TipoDocumentoSerie.Venta, dto.SucursalId, ct);

        var venta = new DomainVenta
        {
            EmpresaId     = dto.EmpresaId,
            SucursalId    = dto.SucursalId,
            RelacionComercialId     = dto.RelacionComercialId,
            NombreCliente = dto.NombreCliente.Trim(),
            TipoPago      = dto.TipoPago,
            Fecha         = dto.Fecha,
            PedidoId      = dto.PedidoId,
            Folio         = folio,
            Observaciones = dto.Observaciones?.Trim(),
            Estatus       = EstatusVenta.Borrador,
            Subtotal      = total,
            Total         = total,
            TotalPagado   = 0m,
            Detalles      = detalles,
        };

        _context.Ventas.Add(venta);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("VentaDocumental {VentaId} creada en Borrador por usuario {UserId}",
            venta.Id, usuarioId);

        return ServiceResult<VentaDocumentalDto>.Ok(MapToDto(venta, []));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<VentaDocumentalDto>> GenerarDesdePedidoAsync(
        long pedidoId, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Venta.Crear, ct))
            return ServiceResult<VentaDocumentalDto>.Fail("Sin permiso (venta.crear).", ErrorCode.Unauthorized);

        if (!await _auth.PuedeAsync(PermisosClave.Pedido.Ver, ct))
            return ServiceResult<VentaDocumentalDto>.Fail("Sin permiso (pedido.ver).", ErrorCode.Unauthorized);

        var pedido = await _context.Pedidos.AsNoTracking()
            .Include(p => p.Detalles)
            .FirstOrDefaultAsync(p => p.Id == pedidoId, ct);

        if (pedido is null)
            return ServiceResult<VentaDocumentalDto>.Fail("Pedido no encontrado.", ErrorCode.NotFound);

        // Guard por estado — Borrador: sin autorización → bloquear; Cancelado: terminal → bloquear.
        // Finalizado: PERMITIDO — es el estado de cumplimiento operacional. La Venta es el documento comercial final.
        if (pedido.Estatus is EstatusPedido.Borrador)
            return ServiceResult<VentaDocumentalDto>.Fail(
                "El pedido debe estar Autorizado antes de generar una venta.", ErrorCode.ValidationFailed);
        if (pedido.Estatus is EstatusPedido.Cancelado)
            return ServiceResult<VentaDocumentalDto>.Fail(
                $"No se puede generar una venta desde un pedido en estado {pedido.Estatus}.", ErrorCode.ValidationFailed);

        // Fix A-002 (Y26): SucursalId no debe asumir Sucursal 1 como fallback.
        // Si el pedido no tiene sucursal asignada, se rechaza para evitar asignación incorrecta.
        if (!pedido.SucursalId.HasValue)
            return ServiceResult<VentaDocumentalDto>.Fail(
                "El pedido no tiene sucursal asignada. Asigna una sucursal antes de generar la venta.",
                ErrorCode.ValidationFailed);

        var dto = new CrearVentaDocumentalDto(
            EmpresaId:    pedido.EmpresaId,
            SucursalId:   pedido.SucursalId.Value,
            RelacionComercialId:    pedido.RelacionComercialId,
            NombreCliente: pedido.NombreCliente,
            TipoPago:     TipoPago.Contado,
            Fecha:        DateTime.Today,
            PedidoId:     pedido.Id,
            Observaciones: pedido.Observaciones,
            Detalles:     pedido.Detalles.Select(d => new CrearDetalleLineaDto(
                d.ProductoId, d.Descripcion, d.Cantidad, d.PrecioUnitario)).ToList());

        var result = await CrearAsync(dto, usuarioId, ct);

        // Consumir anticipos: la venta nace con TotalPagado = AnticipoPagado del Pedido (ADR-065)
        if (result.Success && pedido.AnticipoPagado > 0)
        {
            var venta = await _context.Ventas.FirstOrDefaultAsync(v => v.Id == result.Value!.Id, ct);
            if (venta is not null)
            {
                venta.TotalPagado       = Math.Min(pedido.AnticipoPagado, venta.Total ?? 0);
                venta.FechaModificacion = DateTime.UtcNow;
                venta.UsuarioModificacionId = usuarioId;
                await _context.SaveChangesAsync(ct);

                var reloaded = await ObtenerConDetallesAsync(venta.Id, ct);
                if (reloaded.Success) return reloaded;
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<VentaDocumentalDto>> ActualizarAsync(
        long id, ActualizarVentaDocumentalDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Venta.Editar, ct))
            return ServiceResult<VentaDocumentalDto>.Fail("Sin permiso (venta.editar).", ErrorCode.Unauthorized);

        var venta = await _context.Ventas
            .Include(v => v.Detalles)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

        if (venta is null)
            return ServiceResult<VentaDocumentalDto>.Fail("Venta no encontrada.", ErrorCode.NotFound);

        if (venta.Estatus != EstatusVenta.Borrador)
            return ServiceResult<VentaDocumentalDto>.Fail(
                "Solo se puede editar una venta en estado Borrador.", ErrorCode.ValidationFailed);

        venta.RelacionComercialId     = dto.RelacionComercialId;
        venta.NombreCliente = dto.NombreCliente.Trim();
        venta.TipoPago      = dto.TipoPago;
        venta.Fecha         = dto.Fecha;
        venta.Observaciones = dto.Observaciones?.Trim();

        await _context.SaveChangesAsync(ct);
        var pagos = await _context.Set<DomainPagoVenta>().AsNoTracking()
            .Where(p => p.VentaId == id).ToListAsync(ct);
        return ServiceResult<VentaDocumentalDto>.Ok(MapToDto(venta, pagos));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<VentaDocumentalDto>> ConfirmarAsync(
        long ventaId, int almacenId, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Venta.Confirmar, ct))
            return ServiceResult<VentaDocumentalDto>.Fail("Sin permiso (venta.confirmar).", ErrorCode.Unauthorized);

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var venta = await _context.Ventas
                .Include(v => v.Detalles)
                .FirstOrDefaultAsync(v => v.Id == ventaId, ct);

            if (venta is null)
                return ServiceResult<VentaDocumentalDto>.Fail("Venta no encontrada.", ErrorCode.NotFound);

            if (venta.Estatus != EstatusVenta.Borrador)
                return ServiceResult<VentaDocumentalDto>.Fail(
                    "Solo se puede confirmar una venta en estado Borrador.", ErrorCode.ValidationFailed);

            // Guard A-003 (Y26): no permitir confirmar una venta sin líneas de detalle
            if (!venta.Detalles.Any(d => !d.Borrado))
                return ServiceResult<VentaDocumentalDto>.Fail(
                    "La venta no tiene líneas de detalle. Agrega al menos un producto antes de confirmar.",
                    ErrorCode.ValidationFailed);

            // ── Descontar inventario por línea ──────────────────────────────────
            var tipoMov = ApplicationConstants.TipoMovimientoInventario.SalidaVenta;
            var ahora   = DateTime.UtcNow;

            foreach (var det in venta.Detalles.Where(d => d.ProductoId > 0))
            {
                var existencia = await _context.Existencias
                    .FirstOrDefaultAsync(e => e.ProductoId == det.ProductoId
                                           && e.AlmacenId  == almacenId, ct);
                if (existencia is null)
                {
                    _logger.LogWarning("Producto {ProductoId} sin existencia en almacén {AlmacenId}. Continuando.",
                        det.ProductoId, almacenId);
                }
                else
                {
                    existencia.Cantidad -= det.Cantidad;
                }

                _context.MovimientosInventario.Add(new Ybridio.Domain.Inventario.MovimientoInventario
                {
                    EmpresaId        = venta.EmpresaId,
                    ProductoId       = det.ProductoId,
                    AlmacenId        = almacenId,
                    TipoMovimientoId = tipoMov,
                    Cantidad         = -det.Cantidad,
                    CostoUnitario    = det.Precio ?? 0m,
                    Total            = -(det.Importe ?? 0m),
                    Referencia       = $"Venta#{venta.Id}",
                    ReferenciaId     = venta.Id,
                    Fecha            = ahora,
                    UsuarioCreacionId = usuarioId,
                });

                det.AlmacenId = almacenId;
            }

            venta.Estatus = EstatusVenta.PendientePago;
            await _context.SaveChangesAsync(ct);

            // ── Generar CxC si Crédito ──────────────────────────────────────────
            if (venta.TipoPago == TipoPago.Credito)
            {
                var cxcDto = new DTOs.Finanzas.CrearCxCDto(
                    EmpresaId:       venta.EmpresaId,
                    SucursalId:      venta.SucursalId,
                    NombreDeudor:    venta.NombreCliente ?? "Cliente",
                    Concepto:        $"Venta #{venta.Id}",
                    MontoOriginal:   venta.Total ?? 0m,
                    FechaEmision:    DateTime.Today,
                    // Vencimiento = 30 días (PYME simple, configurable en V2)
                    FechaVencimiento: DateTime.Today.AddDays(30),
                    Observaciones:   venta.Observaciones);

                var cxcResult = await _cxcService.CrearAsync(cxcDto, usuarioId, ct);
                if (!cxcResult.Success)
                    _logger.LogWarning("CxC no generada para Venta {VentaId}: {Error}", venta.Id, cxcResult.Error);
            }

            await tx.CommitAsync(ct);
            _logger.LogInformation("VentaDocumental {VentaId} confirmada. Inventario descontado.", venta.Id);

            var pagos = await _context.Set<DomainPagoVenta>().AsNoTracking()
                .Where(p => p.VentaId == ventaId).ToListAsync(ct);
            return ServiceResult<VentaDocumentalDto>.Ok(MapToDto(venta, pagos));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<PagoVentaDto>> RegistrarPagoAsync(
        RegistrarPagoVentaDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (!await _auth.PuedeAsync(PermisosClave.Pago.Registrar, ct))
            return ServiceResult<PagoVentaDto>.Fail("Sin permiso (pago.registrar).", ErrorCode.Unauthorized);

        if (dto.Monto <= 0)
            return ServiceResult<PagoVentaDto>.Fail("El monto del pago debe ser mayor a cero.", ErrorCode.ValidationFailed);

        var venta = await _context.Ventas.FirstOrDefaultAsync(v => v.Id == dto.VentaId, ct);
        if (venta is null)
            return ServiceResult<PagoVentaDto>.Fail("Venta no encontrada.", ErrorCode.NotFound);

        if (venta.Estatus == EstatusVenta.Cancelada)
            return ServiceResult<PagoVentaDto>.Fail("No se puede registrar pago en una venta cancelada.", ErrorCode.ValidationFailed);

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var pago = new DomainPagoVenta
            {
                VentaId   = dto.VentaId,
                Fecha     = DateTime.UtcNow,
                Monto     = dto.Monto,
                FormaPago = string.IsNullOrWhiteSpace(dto.FormaPago) ? "Efectivo" : dto.FormaPago.Trim(),
                Referencia = dto.Referencia?.Trim(),
            };
            _context.Set<DomainPagoVenta>().Add(pago);
            venta.TotalPagado += dto.Monto;

            // Auto-transición: si saldo = 0 y estaba PendientePago → Pagada
            if (venta.Estatus == EstatusVenta.PendientePago
                && venta.TotalPagado >= (venta.Total ?? 0m))
            {
                venta.Estatus = EstatusVenta.Pagada;
                _logger.LogInformation("VentaDocumental {VentaId} auto-transición PendientePago → Pagada", venta.Id);
            }

            await _context.SaveChangesAsync(ct);

            // ── Si Crédito, buscar y actualizar CxC correspondiente ─────────────
            if (venta.TipoPago == TipoPago.Credito)
            {
                var cxc = await _context.CuentasPorCobrar
                    .FirstOrDefaultAsync(c => c.Concepto == $"Venta #{venta.Id}"
                                           && c.EmpresaId == venta.EmpresaId, ct);
                if (cxc is not null)
                {
                    var pagoResult = await _cxcService.RegistrarPagoAsync(
                        new DTOs.Finanzas.RegistrarPagoCxCDto(cxc.Id, dto.Monto), usuarioId, ct);
                    if (!pagoResult.Success)
                        _logger.LogWarning("CxC no actualizada para Venta {VentaId}: {Error}", venta.Id, pagoResult.Error);
                }
            }

            await tx.CommitAsync(ct);
            return ServiceResult<PagoVentaDto>.Ok(MapPago(pago));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> CancelarAsync(long ventaId, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Venta.Cancelar, ct))
            return ServiceResult.Fail("Sin permiso (venta.cancelar).", ErrorCode.Unauthorized);

        var venta = await _context.Ventas.FirstOrDefaultAsync(v => v.Id == ventaId, ct);
        if (venta is null)
            return ServiceResult.Fail("Venta no encontrada.", ErrorCode.NotFound);

        if (venta.Estatus is EstatusVenta.Cerrada or EstatusVenta.Cancelada)
            return ServiceResult.Fail("No se puede cancelar una venta Cerrada o ya Cancelada.", ErrorCode.ValidationFailed);

        venta.Estatus = EstatusVenta.Cancelada;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> CerrarAsync(long ventaId, Guid usuarioId, CancellationToken ct = default)
    {
        // Fase 6 Y26: permiso granular venta.cerrar (antes reutilizaba venta.confirmar erróneamente)
        if (!await _auth.PuedeAsync(PermisosClave.Venta.Cerrar, ct))
            return ServiceResult.Fail("Sin permiso (venta.cerrar).", ErrorCode.Unauthorized);

        var venta = await _context.Ventas.FirstOrDefaultAsync(v => v.Id == ventaId, ct);
        if (venta is null) return ServiceResult.Fail("Venta no encontrada.", ErrorCode.NotFound);

        if (venta.Estatus is EstatusVenta.Cerrada or EstatusVenta.Cancelada)
            return ServiceResult.Fail("La venta ya está Cerrada o Cancelada.", ErrorCode.ValidationFailed);

        var saldo = (venta.Total ?? 0m) - venta.TotalPagado;
        if (saldo > 0)
            return ServiceResult.Fail($"No se puede cerrar la venta con saldo pendiente de {saldo:C2}.", ErrorCode.ValidationFailed);

        venta.Estatus = EstatusVenta.Cerrada;
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("VentaDocumental {VentaId} cerrada por usuario {UserId}", ventaId, usuarioId);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<DetalleLineaDto>> AgregarDetalleAsync(
        long ventaId, CrearDetalleLineaDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Venta.Editar, ct))
            return ServiceResult<DetalleLineaDto>.Fail("Sin permiso (venta.editar).", ErrorCode.Unauthorized);

        var venta = await _context.Ventas.Include(v => v.Detalles)
            .FirstOrDefaultAsync(v => v.Id == ventaId, ct);

        if (venta is null)
            return ServiceResult<DetalleLineaDto>.Fail("Venta no encontrada.", ErrorCode.NotFound);

        if (venta.Estatus != EstatusVenta.Borrador)
            return ServiceResult<DetalleLineaDto>.Fail("Solo se puede editar una venta en Borrador.", ErrorCode.ValidationFailed);

        // Importe = Cantidad × PrecioUnitario
        var importe = dto.Cantidad * dto.PrecioUnitario;
        var det = new DomainVentaDet
        {
            VentaId    = ventaId,
            ProductoId = dto.ProductoId ?? 0,
            Cantidad   = dto.Cantidad,
            Precio     = dto.PrecioUnitario,
            Importe    = importe,
        };
        venta.Detalles.Add(det);
        venta.Subtotal = (venta.Subtotal ?? 0m) + importe;
        venta.Total    = venta.Subtotal;
        await _context.SaveChangesAsync(ct);

        return ServiceResult<DetalleLineaDto>.Ok(new DetalleLineaDto(
            det.Id, dto.ProductoId, dto.Descripcion, dto.Cantidad, dto.PrecioUnitario, importe));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarDetalleAsync(long detalleId, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Venta.Editar, ct))
            return ServiceResult.Fail("Sin permiso (venta.editar).", ErrorCode.Unauthorized);

        var det = await _context.VentasDetalle.Include(d => d.Venta)
            .FirstOrDefaultAsync(d => d.Id == detalleId, ct);

        if (det is null)
            return ServiceResult.Fail("Detalle no encontrado.", ErrorCode.NotFound);

        if (det.Venta.Estatus != EstatusVenta.Borrador)
            return ServiceResult.Fail("Solo se puede editar una venta en Borrador.", ErrorCode.ValidationFailed);

        det.Venta.Subtotal = (det.Venta.Subtotal ?? 0m) - (det.Importe ?? 0m);
        det.Venta.Total    = det.Venta.Subtotal;
        _context.VentasDetalle.Remove(det);
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    private static VentaDocumentalResumenDto MapToResumen(DomainVenta v)
    {
        var total     = v.Total         ?? 0m;
        var pagado    = v.TotalPagado;
        var saldo     = total - pagado; // SaldoPendiente = Total - TotalPagado (runtime)
        return new VentaDocumentalResumenDto(
            v.Id, v.EmpresaId, v.NombreCliente ?? "", v.Estatus,
            EstatusVentaTexto(v.Estatus),
            v.TipoPago, v.Fecha, total, pagado, saldo, v.PedidoId, v.Observaciones,
            Folio: v.Folio);
    }

    private static VentaDocumentalDto MapToDto(DomainVenta v, IEnumerable<DomainPagoVenta> pagos)
    {
        var total  = v.Total    ?? 0m;
        var saldo  = total - v.TotalPagado; // SaldoPendiente calculado runtime
        return new VentaDocumentalDto(
            Id:             v.Id,
            EmpresaId:      v.EmpresaId,
            SucursalId:     v.SucursalId,
            RelacionComercialId:      v.RelacionComercialId,
            NombreCliente:  v.NombreCliente ?? "",
            Estatus:        v.Estatus,
            EstatusTexto:   EstatusVentaTexto(v.Estatus),
            TipoPago:       v.TipoPago,
            Fecha:          v.Fecha,
            Subtotal:       v.Subtotal ?? total,
            Total:          total,
            TotalPagado:    v.TotalPagado,
            SaldoPendiente: saldo,
            PedidoId:       v.PedidoId,
            Observaciones:  v.Observaciones,
            Detalles:       v.Detalles.Select(d => new DetalleLineaDto(
                d.Id,
                d.ProductoId == 0 ? null : d.ProductoId,
                d.Producto?.Nombre ?? "",       // Nombre desde navegación EF (Include Producto)
                d.Cantidad, d.Precio ?? 0m, d.Importe ?? 0m)).ToList(),
            Pagos:          pagos.Select(MapPago).ToList(),
            Folio:          v.Folio);
    }

    private static PagoVentaDto MapPago(DomainPagoVenta p) =>
        new(p.Id, p.VentaId, p.Fecha, p.Monto, p.FormaPago, p.Referencia);

    internal static string EstatusVentaTexto(EstatusVenta e) => e switch
    {
        EstatusVenta.Borrador      => "Borrador",
        EstatusVenta.PendientePago => "Pendiente de Pago",
        EstatusVenta.Pagada        => "Pagada",
        EstatusVenta.Facturada     => "Facturada",
        EstatusVenta.Entregada     => "Entregada",
        EstatusVenta.Cerrada       => "Cerrada",
        EstatusVenta.Cancelada     => "Cancelada",
        _                          => e.ToString()
    };
}

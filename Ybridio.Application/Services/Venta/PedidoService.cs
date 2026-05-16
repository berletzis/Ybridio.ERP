using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Application.Services.Folios;
using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;
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

        // Fix A-001 (Y26): Include(Cotizacion) reemplaza la subquery N+1 anterior.
        // Una sola query con LEFT JOIN en lugar de O(n) queries adicionales.
        var lista = await query
            .Include(p => p.Cotizacion)
            .OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<PedidoResumenDto>>.Ok(
            lista.Select(p => MapToResumen(p, p.Cotizacion?.Folio)).ToList());
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<PedidoDto>> ObtenerConDetallesAsync(long id, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Pedido.Ver, ct))
            return ServiceResult<PedidoDto>.Fail("Sin permiso (pedido.ver).", ErrorCode.Unauthorized);

        var p = await _context.Pedidos.AsNoTracking()
            .Include(x => x.Detalles).ThenInclude(d => d.Producto)
            .Include(x => x.Cargos)
            .Include(x => x.Anticipos)
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
        // Total con IVA — mismo cálculo que el ViewModel para consistencia financiera
        p.Total                 = RecalcularTotalConIva(p);
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
        // Total con IVA — recalculado tras eliminar el detalle
        d.Pedido.Total                 = RecalcularTotalConIva(d.Pedido);
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

        // Fase 4: OT condicionada por anticipo (ADR-065)
        if (p.AnticipoRequerido.GetValueOrDefault(0) > 0 && p.AnticipoPagado < p.AnticipoRequerido!.Value)
            return ServiceResult<OrdenTrabajoDto>.Fail(
                $"El pedido requiere un anticipo de {p.AnticipoRequerido:C2}. " +
                $"Pagado: {p.AnticipoPagado:C2}. Pendiente: {(p.AnticipoRequerido.Value - p.AnticipoPagado):C2}.",
                ErrorCode.ValidationFailed);

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

        // Total con IVA — consistente con ViewModel
        p.Total = RecalcularTotalConIva(p);
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
        // Total con IVA — recalculado tras eliminar el cargo
        c.Pedido.Total = RecalcularTotalConIva(c.Pedido);
        c.Pedido.FechaModificacion     = DateTime.UtcNow;
        c.Pedido.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // ── Anticipos — dimensión financiera (ADR-065) ───────────────────────────

    /// <inheritdoc/>
    public async Task<ServiceResult<AnticipoPedidoDto>> RegistrarAnticipoAsync(
        long pedidoId, RegistrarAnticipoDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        // Fase 5 Y26: permiso granular anticipo.registrar en lugar de pedido.editar
        if (!await _auth.PuedeAsync(PermisosClave.Anticipo.Registrar, ct))
            return ServiceResult<AnticipoPedidoDto>.Fail("Sin permiso (anticipo.registrar).", ErrorCode.Unauthorized);

        if (dto.Monto <= 0)
            return ServiceResult<AnticipoPedidoDto>.Fail("El monto del anticipo debe ser mayor a cero.", ErrorCode.ValidationFailed);
        if (string.IsNullOrWhiteSpace(dto.FormaPago))
            return ServiceResult<AnticipoPedidoDto>.Fail("La forma de pago es requerida.", ErrorCode.ValidationFailed);

        var p = await _context.Pedidos
            .Include(x => x.Detalles)
            .Include(x => x.Cargos)
            .FirstOrDefaultAsync(x => x.Id == pedidoId, ct);
        if (p is null) return ServiceResult<AnticipoPedidoDto>.Fail("Pedido no encontrado.", ErrorCode.NotFound);
        if (p.Estatus is EstatusPedido.Cancelado)
            return ServiceResult<AnticipoPedidoDto>.Fail("No se puede registrar anticipo en un pedido cancelado.", ErrorCode.ValidationFailed);

        // Recalcular Total con IVA en tiempo real — garantiza comparación correcta
        // (evita falso SobrePagado cuando p.Total en BD no incluía IVA).
        var totalConIva = RecalcularTotalConIva(p);
        p.Total = totalConIva;  // corregir en BD si estaba stale

        // Política Fase 1 Y26 (OPCIÓN B): bloquear pagos adicionales solo si ya hay excedente real.
        var estadoActual = CalcularEstadoFinanciero(p.AnticipoRequerido, p.AnticipoPagado, totalConIva);
        if (estadoActual == EstadoFinancieroPedido.SobrePagado)
        {
            var excedente = Math.Round(p.AnticipoPagado, 2) - Math.Round(totalConIva, 2);
            return ServiceResult<AnticipoPedidoDto>.Fail(
                $"El pedido ya tiene un excedente de {excedente:C2}. No se pueden registrar más anticipos. " +
                "Gestiona el cambio/devolución antes de registrar nuevos pagos.",
                ErrorCode.ValidationFailed);
        }

        var anticipo = new AnticipoPedido
        {
            PedidoId          = pedidoId,
            Fecha             = dto.Fecha ?? DateTime.Today,
            Monto             = dto.Monto,
            FormaPago         = dto.FormaPago.Trim(),
            Referencia        = dto.Referencia?.Trim(),
            FechaCreacion     = DateTime.UtcNow,
            UsuarioCreacionId = usuarioId,
            Borrado           = false
        };

        p.AnticipoPagado          += dto.Monto;
        p.EstadoFinanciero         = CalcularEstadoFinanciero(p.AnticipoRequerido, p.AnticipoPagado, p.Total);
        p.FechaModificacion        = DateTime.UtcNow;
        p.UsuarioModificacionId    = usuarioId;

        _context.AnticipoPedidos.Add(anticipo);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<AnticipoPedidoDto>.Ok(
            new(anticipo.Id, anticipo.PedidoId, anticipo.Fecha, anticipo.Monto,
                anticipo.FormaPago, anticipo.Referencia, usuarioId));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<IReadOnlyList<AnticipoPedidoDto>>> ListarAnticiposAsync(
        long pedidoId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Pedido.Ver, ct))
            return ServiceResult<IReadOnlyList<AnticipoPedidoDto>>.Fail("Sin permiso (pedido.ver).", ErrorCode.Unauthorized);

        var lista = await _context.AnticipoPedidos.AsNoTracking()
            .Where(a => a.PedidoId == pedidoId)
            .OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<AnticipoPedidoDto>>.Ok(
            lista.Select(a => new AnticipoPedidoDto(
                a.Id, a.PedidoId, a.Fecha, a.Monto, a.FormaPago, a.Referencia, a.UsuarioCreacionId))
            .ToList());
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EstablecerAnticipoRequeridoAsync(
        long pedidoId, decimal? monto, Guid usuarioId, CancellationToken ct = default)
    {
        // Fase 5 Y26: permiso granular anticipo.configurar
        if (!await _auth.PuedeAsync(PermisosClave.Anticipo.Configurar, ct))
            return ServiceResult.Fail("Sin permiso (anticipo.configurar).", ErrorCode.Unauthorized);

        if (monto.HasValue && monto.Value < 0)
            return ServiceResult.Fail("El anticipo requerido no puede ser negativo.", ErrorCode.ValidationFailed);

        var p = await _context.Pedidos.FirstOrDefaultAsync(x => x.Id == pedidoId, ct);
        if (p is null) return ServiceResult.Fail("Pedido no encontrado.", ErrorCode.NotFound);
        if (p.Estatus is EstatusPedido.Cancelado or EstatusPedido.Finalizado)
            return ServiceResult.Fail("No se puede modificar el anticipo requerido en estado terminal.", ErrorCode.ValidationFailed);

        p.AnticipoRequerido        = monto;
        p.EstadoFinanciero         = CalcularEstadoFinanciero(monto, p.AnticipoPagado, p.Total);
        p.FechaModificacion        = DateTime.UtcNow;
        p.UsuarioModificacionId    = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Recalcula el Total del Pedido incluyendo IVA (igual que CommercialDocumentCalculator en el ViewModel).
    /// Garantiza que Pedido.Total en BD coincida con lo que el ViewModel muestra al usuario,
    /// evitando el falso SobrePagado cuando los pagos igualan al total con IVA.
    /// </summary>
    private static decimal RecalcularTotalConIva(Pedido p)
    {
        var lineas  = p.Detalles.Select(d => (d.Importe, d.IvaAplicable));
        var cargos  = p.Cargos.Select(c => (c.Importe, c.AplicaIva));
        var todos   = lineas.Concat(cargos).ToList();
        var subtotal = todos.Sum(x => x.Item1);
        var iva      = CommercialDocumentCalculator.CalcularImpuestos(todos, FiscalConstants.TasaIvaEstandar);
        return CommercialDocumentCalculator.CalcularTotal(subtotal, iva);
    }

    /// <summary>
    /// Calcula el <see cref="EstadoFinancieroPedido"/> basado en los montos actuales.
    /// Single Source of Truth para la lógica financiera del pedido.
    /// </summary>
    public static EstadoFinancieroPedido CalcularEstadoFinanciero(
        decimal? anticiRequerido, decimal anticipoPagado, decimal total)
    {
        // Redondear a 2 decimales antes de comparar — evita discrepancias de precisión
        // entre el Total almacenado en BD y el Total calculado en el ViewModel.
        var pagado = Math.Round(anticipoPagado, 2, MidpointRounding.AwayFromZero);
        var tot    = Math.Round(total, 2, MidpointRounding.AwayFromZero);

        // Sobrepago: pagos superiores al total del pedido (comparación con valores redondeados)
        if (tot > 0 && pagado > tot)
            return EstadoFinancieroPedido.SobrePagado;

        // Liquidado: pagos exactamente iguales al total (o dentro del redondeo)
        if (tot > 0 && pagado >= tot)
            return EstadoFinancieroPedido.Liquidado;

        var requerido = anticiRequerido.GetValueOrDefault(0m);
        if (requerido > 0)
        {
            if (anticipoPagado <= 0)           return EstadoFinancieroPedido.SinPago;
            if (anticipoPagado < requerido)    return EstadoFinancieroPedido.AnticipoParcial;
            return EstadoFinancieroPedido.AnticipoCompleto;
        }

        return anticipoPagado > 0
            ? EstadoFinancieroPedido.ParcialmentePagado
            : EstadoFinancieroPedido.SinPago;
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

    private static string EstadoFinancieroTexto(EstadoFinancieroPedido e) => e switch
    {
        EstadoFinancieroPedido.SinPago            => "Sin Pago",
        EstadoFinancieroPedido.AnticipoParcial    => "Anticipo Parcial",
        EstadoFinancieroPedido.AnticipoCompleto   => "Anticipo Completo",
        EstadoFinancieroPedido.ParcialmentePagado => "Parcialmente Pagado",
        EstadoFinancieroPedido.Liquidado          => "Liquidado",
        EstadoFinancieroPedido.SobrePagado        => "Sobre Pagado",
        _                                         => e.ToString()
    };

    private static PedidoResumenDto MapToResumen(Pedido p, string? folioCotizacion = null) =>
        new(p.Id, p.EmpresaId, p.NombreCliente, p.CotizacionId,
            p.Estatus, EstatusTexto(p.Estatus), p.Fecha, p.FechaEntregaCompromiso, p.Total, p.Observaciones,
            Folio:                p.Folio,
            FolioCotizacionOrigen: folioCotizacion,
            AnticipoPagado:       p.AnticipoPagado,
            EstadoFinanciero:     p.EstadoFinanciero,
            EstadoFinancieroTexto: EstadoFinancieroTexto(p.EstadoFinanciero));

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
            Subtotal:              p.Subtotal,
            AnticipoRequerido:     p.AnticipoRequerido,
            AnticipoPagado:        p.AnticipoPagado,
            EstadoFinanciero:      p.EstadoFinanciero,
            EstadoFinancieroTexto: EstadoFinancieroTexto(p.EstadoFinanciero),
            Anticipos:             p.Anticipos.OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id)
                                     .Select(a => new AnticipoPedidoDto(
                                         a.Id, a.PedidoId, a.Fecha, a.Monto,
                                         a.FormaPago, a.Referencia, a.UsuarioCreacionId))
                                     .ToList());
}

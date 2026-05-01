using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Domain.Finanzas;
using Ybridio.Domain.Inventario;
using Ybridio.Infrastructure.Persistence;
using DomainVenta = Ybridio.Domain.Ventas.Venta;
using DomainVentaDetalle = Ybridio.Domain.Ventas.VentaDetalle;

namespace Ybridio.Application.Services.Venta;

/// <summary>
/// Orquesta el proceso completo de venta POS dentro de una única transacción de base de datos.
/// </summary>
public sealed class VentaService : IVentaService
{
    private readonly ErpDbContext _context;
    private readonly ILogger<VentaService> _logger;

    public VentaService(ErpDbContext context, ILogger<VentaService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<VentaDto>> CrearVentaAsync(
        RegistrarVentaDto dto,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var opId = OperationContext.CurrentId;
        _logger.LogInformation(
            "Venta iniciada {OperationId} Usuario:{UsuarioId} Tienda:{TiendaId} Empresa:{EmpresaId}",
            opId, usuarioId, dto.TiendaId, dto.EmpresaId);

        try
        {
            // ── Validaciones defensivas ───────────────────────────────────────────
            if (dto.Detalles is null || dto.Detalles.Count == 0)
                return ServiceResult<VentaDto>.Fail(
                    "La venta debe tener al menos un detalle.",
                    ErrorCode.VentaNoDetalles);

            var itemInvalido = dto.Detalles.FirstOrDefault(d => d.Cantidad <= 0 || d.PrecioUnitario <= 0);
            if (itemInvalido is not null)
                return ServiceResult<VentaDto>.Fail(
                    "Todos los detalles deben tener cantidad y precio mayor a cero.",
                    ErrorCode.ValidationFailed,
                    $"ProductoId:{itemInvalido.ProductoId}");

            // ── 1. Validar que el usuario pertenece a la tienda ───────────────────
            var perteneceATienda = await _context.UsuariosTiendas
                .AsNoTracking()
                .AnyAsync(ut => ut.UsuarioId == usuarioId && ut.TiendaId == dto.TiendaId, ct);

            if (!perteneceATienda)
            {
                _logger.LogWarning(
                    "{OperationId} Usuario {UsuarioId} no pertenece a la tienda {TiendaId}.",
                    opId, usuarioId, dto.TiendaId);
                return ServiceResult<VentaDto>.Fail(
                    "El usuario no pertenece a la tienda indicada.",
                    ErrorCode.VentaUsuarioTiendaMismatch);
            }

            // ── 2. Validar que la caja pertenece a la misma tienda ────────────────
            if (dto.CajaId.HasValue)
            {
                var cajaTiendaId = await _context.Cajas
                    .AsNoTracking()
                    .Where(c => c.Id == dto.CajaId.Value)
                    .Select(c => (int?)c.TiendaId)
                    .FirstOrDefaultAsync(ct);

                if (cajaTiendaId is null)
                    return ServiceResult<VentaDto>.Fail(
                        $"Caja {dto.CajaId} no encontrada.",
                        ErrorCode.CajaNotFound);

                if (cajaTiendaId != dto.TiendaId)
                {
                    _logger.LogWarning(
                        "{OperationId} Caja {CajaId} pertenece a tienda {CajaTienda}, no a tienda {VentaTienda}.",
                        opId, dto.CajaId, cajaTiendaId, dto.TiendaId);
                    return ServiceResult<VentaDto>.Fail(
                        "La caja no pertenece a la tienda de la venta.",
                        ErrorCode.VentaCajaTiendaMismatch);
                }
            }

            // ── 3. Validar apertura de caja activa ────────────────────────────────
            if (dto.AperturaCajaId.HasValue)
            {
                var aperturaCajaActiva = await _context.AperturasCaja
                    .AsNoTracking()
                    .AnyAsync(a => a.Id == dto.AperturaCajaId.Value && a.Activa, ct);

                if (!aperturaCajaActiva)
                {
                    _logger.LogWarning(
                        "{OperationId} AperturaCaja {AperturaCajaId} no está activa.",
                        opId, dto.AperturaCajaId);
                    return ServiceResult<VentaDto>.Fail(
                        "La apertura de caja indicada no está activa.",
                        ErrorCode.VentaCajaNotOpen);
                }
            }

            // ── Inicio de transacción ─────────────────────────────────────────────
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            try
            {
                var ahora = DateTime.UtcNow;

                // ── 4. Construir la entidad Venta (sin SaveChanges aún) ───────────
                var venta = new DomainVenta
                {
                    EmpresaId = dto.EmpresaId,
                    TiendaId = dto.TiendaId,
                    CajaId = dto.CajaId,
                    AperturaCajaId = dto.AperturaCajaId,
                    Fecha = dto.Fecha,
                    FechaCreacion = ahora,
                    UsuarioCreacionId = usuarioId,
                    Borrado = false
                };

                _context.Ventas.Add(venta);

                decimal totalVenta = 0;

                // ── 5 + 6. Construir VentaDetalle, descontar stock y crear kardex ─
                // Se cargan todas las existencias requeridas CON BLOQUEO PESIMISTA
                // (WITH UPDLOCK en SQL Server) dentro de la transacción para evitar
                // condiciones de carrera entre terminales POS concurrentes.
                foreach (var item in dto.Detalles)
                {
                    // Carga pesimista: evita que otro proceso modifique la misma fila antes del commit
                    var existencia = await _context.Existencias
                        .FromSqlRaw(
                            "SELECT * FROM inventario.Existencia WITH (UPDLOCK, ROWLOCK) " +
                            "WHERE EmpresaId = {0} AND ProductoId = {1} AND AlmacenId = {2}",
                            dto.EmpresaId, item.ProductoId, item.AlmacenId)
                        .FirstOrDefaultAsync(ct);

                    if (existencia is null)
                    {
                        _logger.LogWarning(
                            "{OperationId} Sin existencia. Empresa:{EmpresaId} Producto:{ProductoId} Almacén:{AlmacenId}",
                            opId, dto.EmpresaId, item.ProductoId, item.AlmacenId);
                        return ServiceResult<VentaDto>.Fail(
                            $"Producto {item.ProductoId} no tiene existencia en el almacén {item.AlmacenId}.",
                            ErrorCode.ExistenciaNotFound);
                    }

                    if (existencia.Cantidad < item.Cantidad)
                    {
                        _logger.LogWarning(
                            "{OperationId} Stock insuficiente. Producto:{ProductoId} Disponible:{Disponible} Solicitado:{Solicitado}",
                            opId, item.ProductoId, existencia.Cantidad, item.Cantidad);
                        return ServiceResult<VentaDto>.Fail(
                            $"Stock insuficiente para el producto {item.ProductoId}. " +
                            $"Disponible: {existencia.Cantidad}, solicitado: {item.Cantidad}.",
                            ErrorCode.StockInsuficiente,
                            $"ProductoId:{item.ProductoId} Almacen:{item.AlmacenId}");
                    }

                    var importe = item.Cantidad * item.PrecioUnitario;
                    totalVenta += importe;

                    // VentaDetalle relacionado por referencia de objeto — EF resuelve el FK
                    // automáticamente tras el SaveChangesAsync único al final
                    var detalle = new DomainVentaDetalle
                    {
                        Venta = venta,           // relación por objeto, sin necesidad de VentaId
                        ProductoId = item.ProductoId,
                        AlmacenId = item.AlmacenId,
                        Cantidad = item.Cantidad,
                        Precio = item.PrecioUnitario,
                        Importe = importe,
                        FechaCreacion = ahora,
                        UsuarioCreacionId = usuarioId,
                        Borrado = false
                    };

                    _context.VentasDetalle.Add(detalle);

                    // Descuento de stock (la existencia ya está trackeada)
                    existencia.Cantidad -= item.Cantidad;
                    existencia.FechaModificacion = ahora;
                    existencia.UsuarioModificacionId = usuarioId;

                    // Kardex relacionado por referencia de objeto
                    var movInv = new MovimientoInventario
                    {
                        EmpresaId = dto.EmpresaId,
                        AlmacenId = item.AlmacenId,
                        ProductoId = item.ProductoId,
                        TipoMovimientoId = ApplicationConstants.TipoMovimientoInventario.SalidaVenta,
                        Cantidad = item.Cantidad,
                        CostoUnitario = item.PrecioUnitario,
                        Total = importe,
                        Referencia = "VEN",
                        ReferenciaId = null,     // EF no puede resolver long? shadow FK aquí; se actualiza post-commit si es necesario
                        Fecha = dto.Fecha,
                        FechaCreacion = ahora,
                        UsuarioCreacionId = usuarioId,
                        Borrado = false
                    };

                    _context.MovimientosInventario.Add(movInv);
                }

                venta.Total = totalVenta;

                // ── 7. Movimiento de caja ─────────────────────────────────────────
                if (dto.CajaId.HasValue)
                {
                    _context.MovimientosCaja.Add(new MovimientoCaja
                    {
                        CajaId = dto.CajaId.Value,
                        TipoMovimientoId = ApplicationConstants.TipoMovimientoCaja.Venta,
                        Tipo = "VENTA",
                        Monto = totalVenta,
                        Fecha = dto.Fecha,
                        Referencia = null,       // sin VentaId aún; si se requiere, actualizar post-commit
                        FechaCreacion = ahora,
                        UsuarioCreacionId = usuarioId,
                        Borrado = false
                    });
                }

                // ── Único SaveChanges para toda la operación ──────────────────────
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation(
                    "{OperationId} Venta {VentaId} creada. Total:{Total} Usuario:{UsuarioId}",
                    opId, venta.Id, totalVenta, usuarioId);

                return ServiceResult<VentaDto>.Ok(MapToDto(venta));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogWarning(ex,
                    "{OperationId} Conflicto de concurrencia. Usuario:{UsuarioId} Tienda:{TiendaId}",
                    opId, usuarioId, dto.TiendaId);
                return ServiceResult<VentaDto>.Fail(
                    "Conflicto de concurrencia: el inventario fue modificado por otro proceso. Intenta de nuevo.",
                    ErrorCode.ConcurrencyConflict);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex,
                    "{OperationId} Error inesperado al crear venta. Usuario:{UsuarioId} Tienda:{TiendaId}",
                    opId, usuarioId, dto.TiendaId);
                return ServiceResult<VentaDto>.Fail(
                    "Error inesperado al procesar la venta.",
                    ErrorCode.Unknown);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "{OperationId} Error inesperado (previo a transacción). Usuario:{UsuarioId}",
                opId, usuarioId);
            return ServiceResult<VentaDto>.Fail("Error inesperado.", ErrorCode.Unknown);
        }
        finally
        {
            OperationContext.Clear();
        }
    }


    /// <inheritdoc/>
    public async Task<ServiceResult<VentaDto>> ObtenerPorIdAsync(
        long ventaId, CancellationToken ct = default)
    {
        var venta = await _context.Ventas
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == ventaId, ct);

        if (venta is null)
            return ServiceResult<VentaDto>.Fail("Venta no encontrada.", ErrorCode.VentaNotFound);

        return ServiceResult<VentaDto>.Ok(MapToDto(venta));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<VentaDto>> ListarPorEmpresaAsync(
        int empresaId,
        DateTime desde,
        DateTime hasta,
        CancellationToken ct = default)
    {
        return await _context.Ventas
            .AsNoTracking()
            .Where(v => v.EmpresaId == empresaId && v.Fecha >= desde && v.Fecha <= hasta)
            .OrderByDescending(v => v.Fecha)
            .Select(v => new VentaDto(
                v.Id,
                v.EmpresaId,
                v.TiendaId,
                v.Tienda.Nombre,
                v.Fecha,
                v.Total ?? 0,
                v.CajaId,
                v.AperturaCajaId))
            .ToListAsync(ct);
    }

    // ── mapeo interno ──────────────────────────────────────────────────────────

    private static VentaDto MapToDto(DomainVenta v) =>
        new(v.Id, v.EmpresaId, v.TiendaId, string.Empty, v.Fecha, v.Total ?? 0, v.CajaId, v.AperturaCajaId);
}

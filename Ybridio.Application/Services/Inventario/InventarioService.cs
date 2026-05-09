using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Inventario;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Domain.Inventario;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Inventario;

/// <summary>
/// Operaciones de inventario: validación de stock, descuento, kardex y consulta de existencias con enforcement runtime.
/// </summary>
public sealed class InventarioService : IInventarioService
{
    private readonly ErpDbContext               _context;
    private readonly ILogger<InventarioService> _logger;
    private readonly IErpAuthorizationService   _auth;
    private readonly ISecurityScopeResolver     _scopeResolver;
    private readonly ISessionContext            _session;

    public InventarioService(
        ErpDbContext               context,
        ILogger<InventarioService> logger,
        IErpAuthorizationService   auth,
        ISecurityScopeResolver     scopeResolver,
        ISessionContext            session)
    {
        _context       = context;
        _logger        = logger;
        _auth          = auth;
        _scopeResolver = scopeResolver;
        _session       = session;
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<bool>> ValidarStockAsync(
        int empresaId,
        int productoId,
        int almacenId,
        decimal cantidad,
        CancellationToken ct = default)
    {
        var existencia = await _context.Existencias
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.EmpresaId == empresaId
                  && e.ProductoId == productoId
                  && e.AlmacenId == almacenId,
                ct);

        if (existencia is null)
            return ServiceResult<bool>.Ok(false);

        return ServiceResult<bool>.Ok(existencia.Cantidad >= cantidad);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> DescontarInventarioAsync(
        int empresaId,
        int productoId,
        int almacenId,
        decimal cantidad,
        int tipoMovimientoId,
        string referencia,
        long? referenciaId,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var opId = OperationContext.CurrentId;
        _logger.LogInformation(
            "{OperationId} Descontando inventario. Empresa:{EmpresaId} Producto:{ProductoId} Almacen:{AlmacenId} Cantidad:{Cantidad}",
            opId, empresaId, productoId, almacenId, cantidad);

        try
        {
            if (cantidad <= 0)
                return ServiceResult.Fail(
                    "La cantidad a descontar debe ser mayor a cero.",
                    ErrorCode.ValidationFailed,
                    $"ProductoId:{productoId} Cantidad:{cantidad}");

            var existencia = await _context.Existencias
                .FirstOrDefaultAsync(
                    e => e.EmpresaId == empresaId
                      && e.ProductoId == productoId
                      && e.AlmacenId == almacenId,
                    ct);

            if (existencia is null)
            {
                _logger.LogWarning(
                    "{OperationId} Existencia no encontrada. Producto:{ProductoId} Almacen:{AlmacenId}",
                    opId, productoId, almacenId);
                return ServiceResult.Fail(
                    $"No existe registro de existencia para el producto {productoId} en almacén {almacenId}.",
                    ErrorCode.ExistenciaNotFound);
            }

            if (existencia.Cantidad < cantidad)
            {
                _logger.LogWarning(
                    "{OperationId} Stock insuficiente. Producto:{ProductoId} Disponible:{Disponible} Solicitado:{Solicitado}",
                    opId, productoId, existencia.Cantidad, cantidad);
                return ServiceResult.Fail(
                    $"Stock insuficiente. Disponible: {existencia.Cantidad}, solicitado: {cantidad}.",
                    ErrorCode.StockInsuficiente,
                    $"ProductoId:{productoId} Almacen:{almacenId}");
            }

            var ahora = DateTime.UtcNow;
            existencia.Cantidad -= cantidad;
            existencia.FechaModificacion = ahora;
            existencia.UsuarioModificacionId = usuarioId;

            var kardex = new MovimientoInventario
            {
                EmpresaId = empresaId,
                AlmacenId = almacenId,
                ProductoId = productoId,
                TipoMovimientoId = tipoMovimientoId,
                Cantidad = cantidad,
                CostoUnitario = 0,
                Total = 0,
                Referencia = referencia,
                ReferenciaId = referenciaId,
                Fecha = ahora,
                FechaCreacion = ahora,
                UsuarioCreacionId = usuarioId,
                Borrado = false
            };

            _context.MovimientosInventario.Add(kardex);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "{OperationId} Inventario descontado. Producto:{ProductoId} Nueva cantidad:{NuevaCantidad}",
                opId, productoId, existencia.Cantidad);

            return ServiceResult.Ok();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "{OperationId} Conflicto de concurrencia al descontar inventario. Producto:{ProductoId}",
                opId, productoId);
            return ServiceResult.Fail(
                "Conflicto de concurrencia al descontar inventario. Intenta de nuevo.",
                ErrorCode.ConcurrencyConflict);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "{OperationId} Error inesperado al descontar inventario. Producto:{ProductoId}",
                opId, productoId);
            return ServiceResult.Fail("Error inesperado al descontar inventario.", ErrorCode.Unknown);
        }
        finally
        {
            OperationContext.Clear();
        }
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> RegistrarKardexAsync(
        int empresaId,
        int productoId,
        int almacenId,
        int tipoMovimientoId,
        decimal cantidad,
        decimal costoUnitario,
        string referencia,
        long? referenciaId,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        try
        {
            if (cantidad <= 0)
                return ServiceResult.Fail(
                    "La cantidad del kardex debe ser mayor a cero.",
                    ErrorCode.ValidationFailed,
                    $"ProductoId:{productoId} Cantidad:{cantidad}");

            var ahora = DateTime.UtcNow;

            var kardex = new MovimientoInventario
            {
                EmpresaId = empresaId,
                AlmacenId = almacenId,
                ProductoId = productoId,
                TipoMovimientoId = tipoMovimientoId,
                Cantidad = cantidad,
                CostoUnitario = costoUnitario,
                Total = cantidad * costoUnitario,
                Referencia = referencia,
                ReferenciaId = referenciaId,
                Fecha = ahora,
                FechaCreacion = ahora,
                UsuarioCreacionId = usuarioId,
                Borrado = false
            };

            _context.MovimientosInventario.Add(kardex);
            await _context.SaveChangesAsync(ct);

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al registrar kardex. Producto:{ProductoId}", productoId);
            return ServiceResult.Fail("Error inesperado al registrar el kardex.", ErrorCode.Unknown);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ExistenciaDto>> ListarExistenciasAsync(
        int empresaId,
        int? almacenId = null,
        CancellationToken ct = default)
    {
        var query = _context.Existencias
            .AsNoTracking()
            .Where(e => e.EmpresaId == empresaId);

        if (almacenId.HasValue)
            query = query.Where(e => e.AlmacenId == almacenId.Value);

        return await query
            .Select(e => new ExistenciaDto(
                (int)e.Id,
                e.EmpresaId,
                e.AlmacenId,
                e.Almacen.Nombre,
                e.ProductoId,
                e.Producto.Codigo,
                e.Producto.Nombre,
                e.Cantidad))
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<IReadOnlyList<ExistenciaDto>>> ListarExistenciasSeguraAsync(
        int empresaId,
        int? almacenId = null,
        CancellationToken ct = default)
    {
        // ── Permiso ────────────────────────────────────────────────────────────
        if (!await _auth.PuedeAsync(PermisosClave.Existencia.Ver, ct))
            return ServiceResult<IReadOnlyList<ExistenciaDto>>.Fail(
                "Sin permiso para ver existencias (existencia.ver).", ErrorCode.Unauthorized);

        // ── Scope de almacén ───────────────────────────────────────────────────
        if (_session.UsuarioId is { } uid)
        {
            if (almacenId.HasValue)
            {
                // Almacén específico solicitado — validar acceso directo
                if (!await _scopeResolver.TieneAccesoAlmacenAsync(uid, almacenId.Value, ct))
                    return ServiceResult<IReadOnlyList<ExistenciaDto>>.Fail(
                        "Sin acceso al almacén indicado.", ErrorCode.Unauthorized);
            }
            else
            {
                // Sin almacén específico — filtrar por almacenes permitidos del usuario
                var almacenesPermitidos = await _scopeResolver.ObtenerAlmacentesPermitidosAsync(uid, ct);
                if (almacenesPermitidos.Count > 0)
                {
                    // Hay restricciones de almacén → aplicar filtro
                    var result = await _context.Existencias
                        .AsNoTracking()
                        .Where(e => e.EmpresaId == empresaId
                                 && almacenesPermitidos.Contains(e.AlmacenId))
                        .Select(e => new ExistenciaDto(
                            (int)e.Id,
                            e.EmpresaId,
                            e.AlmacenId,
                            e.Almacen.Nombre,
                            e.ProductoId,
                            e.Producto.Codigo,
                            e.Producto.Nombre,
                            e.Cantidad))
                        .ToListAsync(ct);
                    return ServiceResult<IReadOnlyList<ExistenciaDto>>.Ok(result);
                }
                // Lista vacía = sin restricción (SuperAdmin o usuario sin almacenes explícitos)
            }
        }

        // ── Consulta sin restricción de almacén (o almacén específico ya validado) ──
        var query = _context.Existencias
            .AsNoTracking()
            .Where(e => e.EmpresaId == empresaId);

        if (almacenId.HasValue)
            query = query.Where(e => e.AlmacenId == almacenId.Value);

        var lista = await query
            .Select(e => new ExistenciaDto(
                (int)e.Id,
                e.EmpresaId,
                e.AlmacenId,
                e.Almacen.Nombre,
                e.ProductoId,
                e.Producto.Codigo,
                e.Producto.Nombre,
                e.Cantidad))
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<ExistenciaDto>>.Ok(lista);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MovimientoInventarioDto>> ListarKardexAsync(
        int empresaId,
        int productoId,
        DateTime desde,
        DateTime hasta,
        CancellationToken ct = default)
    {
        return await _context.MovimientosInventario
            .AsNoTracking()
            .Where(m => m.EmpresaId == empresaId
                     && m.ProductoId == productoId
                     && m.Fecha >= desde
                     && m.Fecha <= hasta)
            .OrderBy(m => m.Fecha)
            .Select(m => new MovimientoInventarioDto(
                (int)m.Id,
                m.EmpresaId,
                m.ProductoId,
                m.Producto.Nombre,
                m.AlmacenId,
                m.Almacen.Nombre,
                m.TipoMovimientoId,
                m.TipoMovimiento.Nombre,
                m.Cantidad,
                m.Fecha,
                m.Referencia))
            .ToListAsync(ct);
    }
}

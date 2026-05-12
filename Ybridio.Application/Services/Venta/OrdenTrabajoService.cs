using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Domain.Ventas;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Venta;

/// <summary>
/// Servicio de gestión de órdenes de trabajo operativas PYME.
/// </summary>
/// <remarks>
/// EsUrgente en OTResumenDto se determina runtime:
/// EsUrgente = FechaCompromiso.HasValue AND FechaCompromiso.Value.Date &lt;= DateTime.Today.AddDays(1)
/// No se persiste porque depende de la fecha actual.
///
/// Total de la OT se recalcula y persiste en BD al agregar/quitar materiales:
/// Total = SUM(OrdenTrabajoMaterial.Importe)
/// </remarks>
public sealed class OrdenTrabajoService : IOrdenTrabajoService
{
    private readonly ErpDbContext             _context;
    private readonly IErpAuthorizationService _auth;

    public OrdenTrabajoService(ErpDbContext context, IErpAuthorizationService auth)
    {
        _context = context;
        _auth    = auth;
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<IReadOnlyList<OTResumenDto>>> ListarAsync(
        int empresaId, EstatusOrdenTrabajo? estatus = null,
        DateTime? desde = null, DateTime? hasta = null, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.OrdenTrabajo.Ver, ct))
            return ServiceResult<IReadOnlyList<OTResumenDto>>.Fail(
                "Sin permiso para ver órdenes de trabajo (ordentrabajo.ver).", ErrorCode.Unauthorized);

        var query = _context.OrdenesTrabajo.AsNoTracking().Where(o => o.EmpresaId == empresaId);
        if (estatus.HasValue) query = query.Where(o => o.Estatus == estatus.Value);
        if (desde.HasValue)   query = query.Where(o => o.Fecha >= desde.Value);
        if (hasta.HasValue)   query = query.Where(o => o.Fecha <= hasta.Value);

        var lista = await query.OrderByDescending(o => o.Fecha).ThenByDescending(o => o.Id).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<OTResumenDto>>.Ok(lista.Select(MapToResumen).ToList());
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<OrdenTrabajoDto>> ObtenerConMaterialesAsync(long id, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.OrdenTrabajo.Ver, ct))
            return ServiceResult<OrdenTrabajoDto>.Fail("Sin permiso (ordentrabajo.ver).", ErrorCode.Unauthorized);

        var ot = await _context.OrdenesTrabajo.AsNoTracking()
            .Include(o => o.Materiales)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        return ot is null
            ? ServiceResult<OrdenTrabajoDto>.Fail("Orden de trabajo no encontrada.", ErrorCode.NotFound)
            : ServiceResult<OrdenTrabajoDto>.Ok(MapToDto(ot));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<OrdenTrabajoDto>> CrearAsync(CrearOrdenTrabajoDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.OrdenTrabajo.Crear, ct))
            return ServiceResult<OrdenTrabajoDto>.Fail("Sin permiso para crear OT (ordentrabajo.crear).", ErrorCode.Unauthorized);

        if (string.IsNullOrWhiteSpace(dto.Descripcion))
            return ServiceResult<OrdenTrabajoDto>.Fail("La descripción del trabajo es obligatoria.", ErrorCode.ValidationFailed);

        var ot = new OrdenTrabajo
        {
            EmpresaId         = dto.EmpresaId,
            SucursalId        = dto.SucursalId,
            RelacionComercialId         = dto.RelacionComercialId,
            NombreCliente     = dto.NombreCliente.Trim(),
            PedidoId          = dto.PedidoId,
            Estatus           = EstatusOrdenTrabajo.Nueva,
            Fecha             = dto.Fecha,
            FechaCompromiso   = dto.FechaCompromiso,
            Descripcion       = dto.Descripcion.Trim(),
            Observaciones     = dto.Observaciones?.Trim(),
            ResponsableId     = dto.ResponsableId,
            Total             = 0,  // Sin materiales al crear; se recalcula al agregar
            FechaCreacion     = DateTime.UtcNow,
            UsuarioCreacionId = usuarioId,
            Borrado           = false
        };

        _context.OrdenesTrabajo.Add(ot);
        await _context.SaveChangesAsync(ct);
        return ServiceResult<OrdenTrabajoDto>.Ok(MapToDto(ot));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> CambiarEstatusAsync(long id, EstatusOrdenTrabajo nuevoEstatus, Guid usuarioId, CancellationToken ct = default)
    {
        var esCierre = nuevoEstatus is EstatusOrdenTrabajo.Terminada or EstatusOrdenTrabajo.Entregada;
        var permiso  = esCierre ? PermisosClave.OrdenTrabajo.Cerrar : PermisosClave.OrdenTrabajo.Actualizar;

        if (!await _auth.PuedeAsync(permiso, ct))
            return ServiceResult.Fail($"Sin permiso ({permiso}).", ErrorCode.Unauthorized);

        var ot = await _context.OrdenesTrabajo.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (ot is null) return ServiceResult.Fail("Orden de trabajo no encontrada.", ErrorCode.NotFound);
        if (ot.Estatus == EstatusOrdenTrabajo.Cancelada)
            return ServiceResult.Fail("No se puede modificar una OT cancelada.", ErrorCode.ValidationFailed);

        ot.Estatus = nuevoEstatus; ot.FechaModificacion = DateTime.UtcNow; ot.UsuarioModificacionId = usuarioId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<DetalleLineaDto>> AgregarMaterialAsync(long otId, AgregarOTMaterialDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.OrdenTrabajo.Actualizar, ct))
            return ServiceResult<DetalleLineaDto>.Fail("Sin permiso (ordentrabajo.actualizar).", ErrorCode.Unauthorized);

        var ot = await _context.OrdenesTrabajo
            .Include(o => o.Materiales)
            .FirstOrDefaultAsync(o => o.Id == otId, ct);
        if (ot is null) return ServiceResult<DetalleLineaDto>.Fail("OT no encontrada.", ErrorCode.NotFound);

        var material = new OrdenTrabajoMaterial
        {
            OrdenTrabajoId = otId,
            ProductoId     = dto.ProductoId,
            Descripcion    = dto.Descripcion.Trim(),
            Cantidad       = dto.Cantidad,
            PrecioUnitario = dto.PrecioUnitario,
            Importe        = dto.Cantidad * dto.PrecioUnitario  // Importe = Cantidad × PrecioUnitario
        };

        ot.Materiales.Add(material);
        // Recalcular Total de la OT: SUM(materiales.Importe)
        ot.Total = ot.Materiales.Sum(m => m.Importe);
        ot.FechaModificacion = DateTime.UtcNow;
        ot.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<DetalleLineaDto>.Ok(
            new(material.Id, material.ProductoId, material.Descripcion, material.Cantidad, material.PrecioUnitario, material.Importe));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarMaterialAsync(long materialId, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.OrdenTrabajo.Actualizar, ct))
            return ServiceResult.Fail("Sin permiso (ordentrabajo.actualizar).", ErrorCode.Unauthorized);

        var mat = await _context.OrdenTrabajoMateriales
            .Include(m => m.OrdenTrabajo)
            .ThenInclude(ot => ot.Materiales)
            .FirstOrDefaultAsync(m => m.Id == materialId, ct);
        if (mat is null) return ServiceResult.Fail("Material no encontrado.", ErrorCode.NotFound);

        _context.OrdenTrabajoMateriales.Remove(mat);
        // Recalcular Total tras eliminar: SUM(materiales restantes)
        mat.OrdenTrabajo.Total = mat.OrdenTrabajo.Materiales
            .Where(m => m.Id != materialId)
            .Sum(m => m.Importe);
        mat.OrdenTrabajo.FechaModificacion = DateTime.UtcNow;
        mat.OrdenTrabajo.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarAsync(long id, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.OrdenTrabajo.Actualizar, ct))
            return ServiceResult.Fail("Sin permiso (ordentrabajo.actualizar).", ErrorCode.Unauthorized);

        var ot = await _context.OrdenesTrabajo.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (ot is null) return ServiceResult.Fail("OT no encontrada.", ErrorCode.NotFound);

        ot.Borrado = true; ot.FechaModificacion = DateTime.UtcNow; ot.UsuarioModificacionId = usuarioId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<OrdenTrabajoDto>> ActualizarAsync(
        long id, ActualizarOrdenTrabajoDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.OrdenTrabajo.Actualizar, ct))
            return ServiceResult<OrdenTrabajoDto>.Fail("Sin permiso (ordentrabajo.actualizar).", ErrorCode.Unauthorized);

        var ot = await _context.OrdenesTrabajo.Include(o => o.Materiales)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        if (ot is null) return ServiceResult<OrdenTrabajoDto>.Fail("OT no encontrada.", ErrorCode.NotFound);
        if (ot.Estatus is EstatusOrdenTrabajo.Cancelada)
            return ServiceResult<OrdenTrabajoDto>.Fail("No se puede editar una OT cancelada.", ErrorCode.ValidationFailed);

        if (string.IsNullOrWhiteSpace(dto.Descripcion))
            return ServiceResult<OrdenTrabajoDto>.Fail("La descripción del trabajo es obligatoria.", ErrorCode.ValidationFailed);

        ot.RelacionComercialId            = dto.RelacionComercialId;
        ot.NombreCliente        = dto.NombreCliente.Trim();
        ot.Fecha                = dto.Fecha;
        ot.FechaCompromiso      = dto.FechaCompromiso;
        ot.Descripcion          = dto.Descripcion.Trim();
        ot.Observaciones        = dto.Observaciones?.Trim();
        ot.ResponsableId        = dto.ResponsableId;
        ot.FechaModificacion    = DateTime.UtcNow;
        ot.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<OrdenTrabajoDto>.Ok(MapToDto(ot));
    }

    private static string EstatusTexto(EstatusOrdenTrabajo e) => e switch
    {
        EstatusOrdenTrabajo.Nueva             => "Nueva",
        EstatusOrdenTrabajo.EnProceso         => "En Proceso",
        EstatusOrdenTrabajo.EsperandoMaterial => "Esperando Material",
        EstatusOrdenTrabajo.Terminada         => "Terminada",
        EstatusOrdenTrabajo.Entregada         => "Entregada",
        EstatusOrdenTrabajo.Cancelada         => "Cancelada",
        _                                     => e.ToString()
    };

    private static OTResumenDto MapToResumen(OrdenTrabajo o) =>
        new(o.Id, o.EmpresaId, o.NombreCliente, o.Estatus, EstatusTexto(o.Estatus),
            o.Fecha, o.FechaCompromiso, o.Descripcion, o.Total,
            // EsUrgente: runtime — compromiso en las próximas 24h
            o.FechaCompromiso.HasValue && o.FechaCompromiso.Value.Date <= DateTime.Today.AddDays(1)
            && o.Estatus is not (EstatusOrdenTrabajo.Entregada or EstatusOrdenTrabajo.Cancelada));

    private static OrdenTrabajoDto MapToDto(OrdenTrabajo o) =>
        new(o.Id, o.EmpresaId, o.SucursalId, o.RelacionComercialId, o.NombreCliente, o.PedidoId,
            o.Estatus, EstatusTexto(o.Estatus), o.Fecha, o.FechaCompromiso,
            o.Descripcion, o.Observaciones, o.ResponsableId, o.Total,
            o.Materiales.Select(m => new DetalleLineaDto(m.Id, m.ProductoId, m.Descripcion, m.Cantidad, m.PrecioUnitario, m.Importe)).ToList());
}

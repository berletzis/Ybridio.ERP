using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Inventario;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Inventario;

/// <summary>
/// Servicio de consulta y operaciones de salidas de inventario con enforcement de autorización runtime.
/// Valida permiso <c>salida.ver</c> y scope de sucursal antes de retornar datos.
/// Autorización de salida requiere permiso <c>salida.autorizar</c>.
/// </summary>
public sealed class SalidaService : ISalidaService
{
    private readonly ErpDbContext             _context;
    private readonly IErpAuthorizationService _auth;
    private readonly ISecurityScopeResolver   _scopeResolver;
    private readonly ISessionContext          _session;

    public SalidaService(
        ErpDbContext             context,
        IErpAuthorizationService auth,
        ISecurityScopeResolver   scopeResolver,
        ISessionContext          session)
    {
        _context       = context;
        _auth          = auth;
        _scopeResolver = scopeResolver;
        _session       = session;
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<IReadOnlyList<SalidaResumenDto>>> ListarAsync(
        int       empresaId,
        int       sucursalId,
        DateTime? desde  = null,
        DateTime? hasta  = null,
        CancellationToken ct = default)
    {
        // ── Permiso ────────────────────────────────────────────────────────────
        if (!await _auth.PuedeAsync(PermisosClave.Salida.Ver, ct))
            return ServiceResult<IReadOnlyList<SalidaResumenDto>>.Fail(
                "Sin permiso para ver salidas (salida.ver).", ErrorCode.Unauthorized);

        // ── Scope de sucursal ──────────────────────────────────────────────────
        if (_session.UsuarioId is { } uid)
        {
            if (!await _scopeResolver.TieneAccesoSucursalAsync(uid, sucursalId, ct))
                return ServiceResult<IReadOnlyList<SalidaResumenDto>>.Fail(
                    "Sin acceso a la sucursal indicada.", ErrorCode.Unauthorized);
        }

        // ── Consulta ───────────────────────────────────────────────────────────
        var query = _context.Salidas
            .AsNoTracking()
            .Where(s => s.EmpresaId == empresaId && s.SucursalId == sucursalId)
            .Include(s => s.ConceptoSalida)
            .Include(s => s.EstatusSalida)
            .Include(s => s.Almacen)
            .Include(s => s.Detalles)
            .AsQueryable();

        if (desde.HasValue)
            query = query.Where(s => s.Fecha >= desde.Value);
        if (hasta.HasValue)
            query = query.Where(s => s.Fecha <= hasta.Value);

        var lista = await query
            .OrderByDescending(s => s.Fecha)
            .ThenByDescending(s => s.Id)
            .ToListAsync(ct);

        var result = lista.Select(s => new SalidaResumenDto(
            s.Id,
            s.EmpresaId,
            s.SucursalId,
            s.AlmacenId,
            s.Almacen.Nombre,
            s.Folio,
            s.Fecha,
            s.ConceptoSalida.Nombre,
            s.EstatusSalida.Nombre,
            s.Detalles.Count,
            s.Total,
            s.Aplicada,
            s.UsuarioAutorizacionId.HasValue,
            s.Observaciones)).ToList();

        return ServiceResult<IReadOnlyList<SalidaResumenDto>>.Ok(result);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> AutorizarAsync(
        long salidaId,
        Guid usuarioAutorizacionId,
        CancellationToken ct = default)
    {
        // ── Permiso ────────────────────────────────────────────────────────────
        if (!await _auth.PuedeAsync(PermisosClave.Salida.Autorizar, ct))
            return ServiceResult.Fail(
                "Sin permiso para autorizar salidas (salida.autorizar).", ErrorCode.Unauthorized);

        // ── Buscar salida ──────────────────────────────────────────────────────
        var salida = await _context.Salidas
            .FirstOrDefaultAsync(s => s.Id == salidaId, ct);

        if (salida is null)
            return ServiceResult.Fail("Salida no encontrada.", ErrorCode.NotFound);

        // ── Scope de sucursal ──────────────────────────────────────────────────
        if (_session.UsuarioId is { } uid)
        {
            if (!await _scopeResolver.TieneAccesoSucursalAsync(uid, salida.SucursalId, ct))
                return ServiceResult.Fail("Sin acceso a la sucursal de esta salida.", ErrorCode.Unauthorized);
        }

        // ── Ya autorizada ──────────────────────────────────────────────────────
        if (salida.UsuarioAutorizacionId.HasValue)
            return ServiceResult.Fail("La salida ya fue autorizada.", ErrorCode.ValidationFailed);

        salida.UsuarioAutorizacionId = usuarioAutorizacionId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }
}

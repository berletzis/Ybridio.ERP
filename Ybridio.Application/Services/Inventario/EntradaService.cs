using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Inventario;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Inventario;

/// <summary>
/// Servicio de consulta de entradas de inventario con enforcement de autorización runtime.
/// Valida permiso <c>entrada.ver</c> y acceso al scope de sucursal antes de retornar datos.
/// </summary>
public sealed class EntradaService : IEntradaService
{
    private readonly ErpDbContext             _context;
    private readonly IErpAuthorizationService _auth;
    private readonly ISecurityScopeResolver   _scopeResolver;
    private readonly ISessionContext          _session;

    public EntradaService(
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
    public async Task<ServiceResult<IReadOnlyList<EntradaResumenDto>>> ListarAsync(
        int       empresaId,
        int       sucursalId,
        DateTime? desde  = null,
        DateTime? hasta  = null,
        CancellationToken ct = default)
    {
        // ── Permiso ────────────────────────────────────────────────────────────
        if (!await _auth.PuedeAsync(PermisosClave.Entrada.Ver, ct))
            return ServiceResult<IReadOnlyList<EntradaResumenDto>>.Fail(
                "Sin permiso para ver entradas (entrada.ver).", ErrorCode.Unauthorized);

        // ── Scope de sucursal ──────────────────────────────────────────────────
        if (_session.UsuarioId is { } uid)
        {
            if (!await _scopeResolver.TieneAccesoSucursalAsync(uid, sucursalId, ct))
                return ServiceResult<IReadOnlyList<EntradaResumenDto>>.Fail(
                    "Sin acceso a la sucursal indicada.", ErrorCode.Unauthorized);
        }

        // ── Consulta ───────────────────────────────────────────────────────────
        var query = _context.Entradas
            .AsNoTracking()
            .Where(e => e.EmpresaId == empresaId && e.SucursalId == sucursalId)
            .Include(e => e.ConceptoEntrada)
            .Include(e => e.EstatusEntrada)
            .Include(e => e.Almacen)
            .Include(e => e.Detalles)
            .AsQueryable();

        if (desde.HasValue)
            query = query.Where(e => e.Fecha >= desde.Value);
        if (hasta.HasValue)
            query = query.Where(e => e.Fecha <= hasta.Value);

        var lista = await query
            .OrderByDescending(e => e.Fecha)
            .ThenByDescending(e => e.Id)
            .ToListAsync(ct);

        var result = lista.Select(e => new EntradaResumenDto(
            e.Id,
            e.EmpresaId,
            e.SucursalId,
            e.AlmacenId,
            e.Almacen.Nombre,
            e.Folio,
            e.Fecha,
            e.ConceptoEntrada.Nombre,
            e.EstatusEntrada.Nombre,
            e.Detalles.Count,
            e.Total,
            e.Aplicada,
            e.Observaciones)).ToList();

        return ServiceResult<IReadOnlyList<EntradaResumenDto>>.Ok(result);
    }
}

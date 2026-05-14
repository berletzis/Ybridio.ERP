using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Configuracion;

/// <summary>
/// Implementación de <see cref="IConfiguracionFiscalService"/> sobre EF Core.
/// Resuelve la cadena: ParametroGlobal (clave → TipoImpuestoId) → TipoImpuesto (datos fiscales reales).
///
/// ADR-026 — Aislamiento de contexto:
/// Usa <see cref="IDbContextFactory{TContext}"/> en lugar del DbContext scoped
/// para evitar la excepción "A second operation was started on this context instance".
/// Este servicio se llama frecuentemente en modo fire-and-forget desde la UI
/// (CargarConfiguracionFiscalAsync), concurrente con otras operaciones del contexto scoped.
/// Un contexto propio por operación elimina la carrera de datos.
/// </summary>
public sealed class ConfiguracionFiscalService : IConfiguracionFiscalService
{
    private readonly IDbContextFactory<ErpDbContext> _factory;
    private readonly ISessionContext _session;

    public ConfiguracionFiscalService(
        IDbContextFactory<ErpDbContext> factory,
        ISessionContext                 session)
    {
        _factory = factory;
        _session = session;
    }

    /// <inheritdoc/>
    public Task<TipoImpuestoDto?> ObtenerTipoImpuestoProductoAsync(CancellationToken ct = default)
        => ObtenerTipoImpuestoPorClaveAsync(ParametrosClave.Fiscal.ImpuestoDefaultProducto, ct);

    /// <inheritdoc/>
    public async Task<decimal> ObtenerTasaIvaProductoAsync(CancellationToken ct = default)
    {
        var tipo = await ObtenerTipoImpuestoProductoAsync(ct);
        return tipo?.Tasa ?? FiscalConstants.TasaIvaEstandar;
    }

    /// <inheritdoc/>
    public Task<TipoImpuestoDto?> ObtenerTipoImpuestoServicioAsync(CancellationToken ct = default)
        => ObtenerTipoImpuestoPorClaveAsync(ParametrosClave.Fiscal.ImpuestoDefaultServicio, ct);

    /// <inheritdoc/>
    public Task<TipoImpuestoDto?> ObtenerTipoImpuestoCargoAsync(CancellationToken ct = default)
        => ObtenerTipoImpuestoPorClaveAsync(ParametrosClave.Fiscal.ImpuestoDefaultCargo, ct);

    /// <inheritdoc/>
    public async Task<TipoImpuestoDto?> ObtenerTipoImpuestoPorClaveAsync(
        string claveParametro, CancellationToken ct = default)
    {
        // Contexto aislado: NO compartir con el DbContext scoped de la llamada concurrente
        await using var ctx = await _factory.CreateDbContextAsync(ct);

        // 1. Leer el TipoImpuestoId desde ParametroGlobal en el mismo contexto aislado
        var param = await ctx.ParametrosGlobal
            .AsNoTracking()
            .Where(p => p.EmpresaId == _session.EmpresaId
                     && p.Clave     == claveParametro
                     && p.Activo)
            .FirstOrDefaultAsync(ct);

        if (param is null || !int.TryParse(param.Valor, out var idImpuesto) || idImpuesto <= 0)
            return null;

        // 2. Cargar el TipoImpuesto en el mismo contexto aislado
        var tipo = await ctx.TiposImpuesto
            .AsNoTracking()
            .Where(t => t.Id       == idImpuesto
                     && t.EmpresaId == _session.EmpresaId
                     && t.Activo)
            .FirstOrDefaultAsync(ct);

        return tipo is null ? null : MapToDto(tipo);
    }

    private static TipoImpuestoDto MapToDto(TipoImpuesto t) =>
        new(t.Id, t.EmpresaId, t.Nombre, t.Porcentaje, t.Activo,
            t.Codigo, t.TipoGravamen, t.EsExento, t.OrdenVisual, t.Descripcion);
}

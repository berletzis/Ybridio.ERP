using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Domain.Catalogos;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Folios;

/// <summary>
/// Implementación del motor de folios documentales.
///
/// Concurrencia: usa <c>UPDATE ... OUTPUT DELETED.SiguienteNumero</c> de SQL Server
/// para obtener e incrementar el consecutivo en una operación ATÓMICA.
/// Garantiza unicidad bajo múltiples usuarios simultáneos sin locks explícitos.
///
/// Aislamiento: usa IDbContextFactory para obtener un contexto dedicado para la operación atómica,
/// evitando conflictos con el contexto scoped del llamador.
/// </summary>
public sealed class FolioGeneratorService : IFolioGeneratorService
{
    private readonly IDbContextFactory<ErpDbContext> _factory;

    public FolioGeneratorService(IDbContextFactory<ErpDbContext> factory)
        => _factory = factory;

    /// <inheritdoc/>
    public async Task<string?> GenerarFolioAsync(
        int empresaId,
        TipoDocumentoSerie tipo,
        int? sucursalId = null,
        CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);

        // Buscar serie: primero específica de sucursal, luego global de empresa
        var serie = await ctx.SeriesDocumento
            .Where(s => s.EmpresaId == empresaId
                     && s.TipoDocumento == tipo
                     && s.Activo
                     && !s.Borrado)
            .OrderBy(s => s.SucursalId == sucursalId ? 0 : 1)  // prefer specific
            .ThenBy(s => s.SucursalId.HasValue ? 1 : 0)          // then global (null SucursalId)
            .FirstOrDefaultAsync(ct);

        if (serie is null) return null;

        // Operación atómica: UPDATE con OUTPUT — devuelve el número ANTES del incremento.
        // ToListAsync() materializa client-side ANTES de llamar First() para evitar que
        // EF Core intente componer SQL adicional (TOP 1) sobre la sentencia UPDATE,
        // lo que lanza 'SqlQuery was called with non-composable SQL'.
        var resultados = await ctx.Database
            .SqlQuery<long>(
                $"UPDATE catalogos.SerieDocumento SET SiguienteNumero = SiguienteNumero + 1 OUTPUT DELETED.SiguienteNumero WHERE Id = {serie.Id}")
            .ToListAsync(ct);

        if (resultados.Count == 0) return null;
        var numero = resultados[0];

        return FormatearFolio(serie.Prefijo, numero, serie.Longitud);
    }

    /// <inheritdoc/>
    public async Task<string?> ObtenerFolioSiguienteAsync(
        int empresaId,
        TipoDocumentoSerie tipo,
        int? sucursalId = null,
        CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);

        var serie = await ctx.SeriesDocumento
            .AsNoTracking()
            .Where(s => s.EmpresaId == empresaId
                     && s.TipoDocumento == tipo
                     && s.Activo
                     && !s.Borrado)
            .OrderBy(s => s.SucursalId == sucursalId ? 0 : 1)
            .ThenBy(s => s.SucursalId.HasValue ? 1 : 0)
            .FirstOrDefaultAsync(ct);

        if (serie is null) return null;
        return FormatearFolio(serie.Prefijo, serie.SiguienteNumero, serie.Longitud);
    }

    private static string FormatearFolio(string prefijo, long numero, int longitud)
        => $"{prefijo}-{numero.ToString().PadLeft(longitud, '0')}";
}

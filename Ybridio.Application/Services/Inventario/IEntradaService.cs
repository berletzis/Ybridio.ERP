using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Inventario;

namespace Ybridio.Application.Services.Inventario;

/// <summary>
/// Contrato para consulta de entradas de inventario con enforcement de autorización y scopes.
/// Toda operación valida permiso <c>entrada.ver</c> y scope de sucursal en runtime.
/// </summary>
public interface IEntradaService
{
    /// <summary>
    /// Lista entradas de la empresa y sucursal indicadas, dentro del rango de fechas.
    /// Valida: permiso <c>entrada.ver</c> + acceso a la sucursal indicada.
    /// </summary>
    Task<ServiceResult<IReadOnlyList<EntradaResumenDto>>> ListarAsync(
        int       empresaId,
        int       sucursalId,
        DateTime? desde  = null,
        DateTime? hasta  = null,
        CancellationToken ct = default);
}

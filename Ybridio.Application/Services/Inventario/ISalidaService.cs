using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Inventario;

namespace Ybridio.Application.Services.Inventario;

/// <summary>
/// Contrato para consulta y operaciones de salidas de inventario con enforcement de autorización y scopes.
/// </summary>
public interface ISalidaService
{
    /// <summary>
    /// Lista salidas de la empresa y sucursal indicadas, dentro del rango de fechas.
    /// Valida: permiso <c>salida.ver</c> + acceso a la sucursal indicada.
    /// </summary>
    Task<ServiceResult<IReadOnlyList<SalidaResumenDto>>> ListarAsync(
        int       empresaId,
        int       sucursalId,
        DateTime? desde  = null,
        DateTime? hasta  = null,
        CancellationToken ct = default);

    /// <summary>
    /// Autoriza una salida pendiente.
    /// Valida permiso <c>salida.autorizar</c> y acceso al scope de la sucursal destino.
    /// El ID del usuario que autoriza se registra en <c>Salida.UsuarioAutorizacionId</c>.
    /// </summary>
    Task<ServiceResult> AutorizarAsync(
        long salidaId,
        Guid usuarioAutorizacionId,
        CancellationToken ct = default);
}

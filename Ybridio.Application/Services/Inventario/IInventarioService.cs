using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Inventario;

namespace Ybridio.Application.Services.Inventario;

/// <summary>
/// Contrato para operaciones de inventario: consulta de stock, descuento y kardex.
/// </summary>
public interface IInventarioService
{
    /// <summary>
    /// Valida si hay suficiente stock del producto en el almacén indicado.
    /// Retorna <c>true</c> en el Value si hay stock suficiente.
    /// </summary>
    Task<ServiceResult<bool>> ValidarStockAsync(
        int empresaId,
        int productoId,
        int almacenId,
        decimal cantidad,
        CancellationToken ct = default);

    /// <summary>
    /// Descuenta la cantidad indicada del stock del producto y registra el movimiento de kardex.
    /// Usa RowVersion para concurrencia optimista.
    /// </summary>
    Task<ServiceResult> DescontarInventarioAsync(
        int empresaId,
        int productoId,
        int almacenId,
        decimal cantidad,
        int tipoMovimientoId,
        string referencia,
        long? referenciaId,
        Guid usuarioId,
        CancellationToken ct = default);

    /// <summary>
    /// Registra un movimiento de inventario (kardex) sin modificar la existencia.
    /// Útil para ajustes manuales ya procesados externamente.
    /// </summary>
    Task<ServiceResult> RegistrarKardexAsync(
        int empresaId,
        int productoId,
        int almacenId,
        int tipoMovimientoId,
        decimal cantidad,
        decimal costoUnitario,
        string referencia,
        long? referenciaId,
        Guid usuarioId,
        CancellationToken ct = default);

    /// <summary>Lista las existencias de una empresa, con filtro opcional de almacén.</summary>
    Task<IReadOnlyList<ExistenciaDto>> ListarExistenciasAsync(
        int empresaId,
        int? almacenId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lista las existencias con enforcement de autorización y scope de almacén.
    /// Valida permiso <c>existencia.ver</c> y que el usuario tenga acceso al almacén indicado.
    /// Si <paramref name="almacenId"/> es nulo, filtra automáticamente por los almacenes permitidos del usuario.
    /// </summary>
    Task<ServiceResult<IReadOnlyList<ExistenciaDto>>> ListarExistenciasSeguraAsync(
        int empresaId,
        int? almacenId = null,
        CancellationToken ct = default);

    /// <summary>Lista el kardex (movimientos) de un producto en un rango de fechas.</summary>
    Task<IReadOnlyList<MovimientoInventarioDto>> ListarKardexAsync(
        int empresaId,
        int productoId,
        DateTime desde,
        DateTime hasta,
        CancellationToken ct = default);
}

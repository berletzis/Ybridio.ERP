using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Inventario;

namespace Ybridio.Application.Services.Inventario;

/// <summary>
/// Gestión de almacenes físicos o lógicos dentro de una sucursal.
/// Jerarquía: Empresa → Sucursal → Almacén.
/// </summary>
public interface IAlmacenService
{
    /// <summary>
    /// Retorna el almacén marcado como principal de la sucursal indicada.
    /// Retorna Fail si la sucursal no tiene ningún almacén principal configurado.
    /// </summary>
    Task<ServiceResult<AlmacenDto>> ObtenerPrincipalDeSucursalAsync(
        int sucursalId, CancellationToken ct = default);

    /// <summary>Lista todos los almacenes activos de una sucursal.</summary>
    Task<IReadOnlyList<AlmacenDto>> ListarPorSucursalAsync(
        int sucursalId, CancellationToken ct = default);

    /// <summary>Crea un nuevo almacén en la sucursal indicada. Valida nombre no vacío.</summary>
    Task<ServiceResult<AlmacenDto>> CrearAsync(
        CrearAlmacenDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Actualiza nombre, código y descripción de un almacén existente.</summary>
    Task<ServiceResult<AlmacenDto>> ActualizarAsync(
        int id, ActualizarAlmacenDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Marca un almacén como principal de su sucursal.
    /// Desactiva <see cref="AlmacenDto.EsPrincipal"/> en el anterior principal.
    /// Operación atómica en una sola transacción.
    /// </summary>
    Task<ServiceResult> MarcarPrincipalAsync(
        int almacenId, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Alterna <see cref="AlmacenDto.Activo"/>. No permite desactivar el almacén principal.</summary>
    Task<ServiceResult> CambiarActivoAsync(
        int almacenId, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Soft-delete de un almacén. No permite eliminar el almacén principal.</summary>
    Task<ServiceResult> EliminarAsync(
        int almacenId, Guid usuarioId, CancellationToken ct = default);
}

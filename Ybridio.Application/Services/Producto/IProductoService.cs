// ── Ybridio.Application/Services/Producto/IProductoService.cs ────────────────
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Catalogos;

namespace Ybridio.Application.Services.Producto;

/// <summary>
/// Contrato para CRUD de Productos incluyendo clonar y comparar.
/// </summary>
public interface IProductoService
{
    // ── Consultas ─────────────────────────────────────────────────────────────

    /// <summary>Lista todos los productos de la empresa con sus catálogos resueltos.</summary>
    Task<IReadOnlyList<ProductoDto>> ListarPorEmpresaAsync(
        int empresaId,
        bool soloActivos = false,
        CancellationToken ct = default);

    /// <summary>Obtiene un producto por su ID.</summary>
    Task<ServiceResult<ProductoDto>> ObtenerPorIdAsync(
        int productoId,
        CancellationToken ct = default);

    /// <summary>Busca productos por código, código de barras o nombre.</summary>
    Task<IReadOnlyList<ProductoDto>> BuscarAsync(
        int empresaId,
        string termino,
        CancellationToken ct = default);

    // ── Escritura ─────────────────────────────────────────────────────────────

    /// <summary>Crea un nuevo producto.</summary>
    Task<ServiceResult<ProductoDto>> CrearAsync(
        CrearProductoDto dto,
        Guid usuarioId,
        CancellationToken ct = default);

    /// <summary>Actualiza un producto existente.</summary>
    Task<ServiceResult<ProductoDto>> ActualizarAsync(
        int productoId,
        ActualizarProductoDto dto,
        Guid usuarioId,
        CancellationToken ct = default);

    /// <summary>
    /// Clona un producto existente creando uno nuevo con código y nombre distintos.
    /// Copia todos los campos del original excepto los indicados en el DTO.
    /// </summary>
    Task<ServiceResult<ProductoDto>> ClonarAsync(
        ClonarProductoDto dto,
        Guid usuarioId,
        CancellationToken ct = default);

    /// <summary>Activa o desactiva un producto.</summary>
    Task<ServiceResult> CambiarActivoAsync(
        int productoId,
        bool activo,
        Guid usuarioId,
        CancellationToken ct = default);

    /// <summary>Soft-delete de un producto.</summary>
    Task<ServiceResult> EliminarAsync(
        int productoId,
        Guid usuarioId,
        CancellationToken ct = default);

    // ── Catálogos dependientes ────────────────────────────────────────────────

    Task<IReadOnlyList<UnidadMedidaDto>> ListarUnidadesMedidaAsync(int empresaId, CancellationToken ct = default);
    Task<IReadOnlyList<CategoriaProductoDto>> ListarCategoriasAsync(int empresaId, CancellationToken ct = default);
    Task<IReadOnlyList<TipoProductoDto>> ListarTiposProductoAsync(int empresaId, CancellationToken ct = default);
    Task<IReadOnlyList<TipoImpuestoDto>> ListarTiposImpuestoAsync(int empresaId, CancellationToken ct = default);
}

// ── Ybridio.Application/DTOs/Catalogos/ProductoDto.cs — REEMPLAZAR COMPLETO ──

namespace Ybridio.Application.DTOs.Catalogos;

/// <summary>DTO de lectura completo para Producto.</summary>
public sealed record ProductoDto(
    int Id,
    int EmpresaId,
    string Codigo,
    string? CodigoBarras,
    string Nombre,
    string? Descripcion,
    decimal Precio,
    decimal? PrecioMinimo,
    decimal? Costo,
    bool IvaAplicable,
    int? TipoImpuestoId,
    string? TipoImpuestoNombre,
    decimal? TipoImpuestoPorcentaje,
    int? CategoriaId,
    string? CategoriaNombre,
    int? TipoProductoId,
    string? TipoProductoNombre,
    int? UnidadMedidaId,
    string? UnidadMedidaNombre,
    string? UnidadMedidaAbreviatura,
    decimal? StockMinimo,
    decimal? StockMaximo,
    int? ProveedorId,
    string? ProveedorNombre,
    bool Activo,
    /// <summary>
    /// Todos los CategoriaProducto.Id a los que pertenece el producto (relación N:N).
    /// Permite filtrar por cualquier categoría, no solo la principal.
    /// </summary>
    IReadOnlyList<int> CategoriaIds);

/// <summary>DTO para crear un Producto.</summary>
public sealed record CrearProductoDto(
    int EmpresaId,
    string Codigo,
    string? CodigoBarras,
    string Nombre,
    string? Descripcion,
    decimal Precio,
    decimal? PrecioMinimo,
    decimal? Costo,
    bool IvaAplicable,
    int? TipoImpuestoId,
    int? CategoriaId,
    int? TipoProductoId,
    int? UnidadMedidaId,
    decimal? StockMinimo,
    decimal? StockMaximo,
    int? ProveedorId,
    bool Activo = true);

/// <summary>DTO para actualizar un Producto.</summary>
public sealed record ActualizarProductoDto(
    string Codigo,
    string? CodigoBarras,
    string Nombre,
    string? Descripcion,
    decimal Precio,
    decimal? PrecioMinimo,
    decimal? Costo,
    bool IvaAplicable,
    int? TipoImpuestoId,
    int? CategoriaId,
    int? TipoProductoId,
    int? UnidadMedidaId,
    decimal? StockMinimo,
    decimal? StockMaximo,
    int? ProveedorId,
    bool Activo);

/// <summary>DTO para clonar un Producto — solo los campos que cambian.</summary>
public sealed record ClonarProductoDto(
    int ProductoOrigenId,
    string NuevoCodigo,
    string NuevoNombre,
    decimal? NuevoPrecio = null);   // null = usar el mismo precio del original


// ── DTOs para los catálogos nuevos ───────────────────────────────────────────

/// <summary>DTO de lectura para UnidadMedida.</summary>
public sealed record UnidadMedidaDto(
    int Id,
    int EmpresaId,
    string Nombre,
    string Abreviatura,
    bool Activo);

/// <summary>DTO para crear/actualizar UnidadMedida.</summary>
public sealed record UpsertUnidadMedidaDto(
    string Nombre,
    string Abreviatura,
    bool Activo = true);

/// <summary>DTO de lectura para CategoriaProducto.</summary>
public sealed record CategoriaProductoDto(
    int Id,
    int EmpresaId,
    string Nombre,
    string? Descripcion,
    bool Activo);

/// <summary>DTO para crear/actualizar CategoriaProducto.</summary>
public sealed record UpsertCategoriaProductoDto(
    string Nombre,
    string? Descripcion,
    bool Activo = true);

/// <summary>DTO de lectura para TipoProducto.</summary>
public sealed record TipoProductoDto(
    int Id,
    int EmpresaId,
    string Nombre,
    string? Descripcion,
    bool Activo);

/// <summary>DTO para crear/actualizar TipoProducto.</summary>
public sealed record UpsertTipoProductoDto(
    string Nombre,
    string? Descripcion,
    bool Activo = true);

/// <summary>DTO de lectura para TipoImpuesto.</summary>
public sealed record TipoImpuestoDto(
    int Id,
    int EmpresaId,
    string Nombre,
    decimal Porcentaje,
    bool Activo);

/// <summary>DTO para crear/actualizar TipoImpuesto.</summary>
public sealed record UpsertTipoImpuestoDto(
    string Nombre,
    decimal Porcentaje,
    bool Activo = true);

/// <summary>
/// Proyección de categoría con conteo de productos asociados.
/// Usada por el panel de clasificación tipo Outlook.
/// </summary>
public sealed record CategoriaConConteoDto(
    int Id,
    string Nombre,
    int? CategoriaPadreId,
    int TotalProductos);
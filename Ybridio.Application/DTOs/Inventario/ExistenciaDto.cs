namespace Ybridio.Application.DTOs.Inventario;

/// <summary>DTO de lectura para Existencia.</summary>
public sealed record ExistenciaDto(
    int Id,
    int EmpresaId,
    int AlmacenId,
    string AlmacenNombre,
    int ProductoId,
    string ProductoCodigo,
    string ProductoNombre,
    decimal Cantidad);

/// <summary>DTO de lectura para MovimientoInventario.</summary>
public sealed record MovimientoInventarioDto(
    int Id,
    int EmpresaId,
    int ProductoId,
    string ProductoNombre,
    int AlmacenId,
    string AlmacenNombre,
    int TipoMovimientoId,
    string TipoMovimientoNombre,
    decimal Cantidad,
    DateTime Fecha,
    string? Referencia);

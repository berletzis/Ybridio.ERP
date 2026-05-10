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
    decimal Cantidad,
    decimal? StockMinimo = null)
{
    /// <summary>
    /// Estado operacional simple: Normal | Bajo | Agotado.
    /// </summary>
    public string EstadoStock =>
        Cantidad == 0            ? "Agotado" :
        StockMinimo.HasValue &&
        Cantidad <= StockMinimo  ? "Bajo" :
                                   "Normal";
};

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

/// <summary>
/// DTO enriquecido para la vista operacional de Kardex.
/// Incluye saldo acumulado, trazabilidad documental, sucursal y observaciones.
/// </summary>
public sealed record KardexLineaDto(
    long Id,
    int EmpresaId,
    int ProductoId,
    string ProductoCodigo,
    string ProductoNombre,
    int AlmacenId,
    string AlmacenNombre,
    int? SucursalId,
    string? SucursalNombre,
    int TipoMovimientoId,
    string TipoMovimientoNombre,
    /// <summary>1 = entrada, -1 = salida.</summary>
    short Signo,
    decimal Cantidad,
    /// <summary>Cantidad como entrada (positiva si Signo == 1, cero si es salida).</summary>
    decimal Entrada,
    /// <summary>Cantidad como salida (positiva si Signo == -1, cero si es entrada).</summary>
    decimal Salida,
    decimal SaldoAcumulado,
    decimal CostoUnitario,
    DateTime Fecha,
    string? Folio,
    string? Referencia,
    long? ReferenciaId,
    string? Observaciones,
    string? UsuarioNombre);


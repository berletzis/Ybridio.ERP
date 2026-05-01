namespace Ybridio.Application.DTOs.Ventas;

/// <summary>DTO de lectura para Venta.</summary>
public sealed record VentaDto(
    long Id,
    int EmpresaId,
    int TiendaId,
    string TiendaNombre,
    DateTime Fecha,
    decimal Total,
    int? CajaId,
    int? AperturaCajaId);

/// <summary>DTO para registrar una Venta (input POS).</summary>
public sealed record RegistrarVentaDto(
    int EmpresaId,
    int TiendaId,
    int? CajaId,
    int? AperturaCajaId,
    DateTime Fecha,
    IReadOnlyList<RegistrarVentaDetalleDto> Detalles);

/// <summary>DTO de detalle de entrada para una Venta.</summary>
public sealed record RegistrarVentaDetalleDto(
    int ProductoId,
    int AlmacenId,
    decimal Cantidad,
    decimal PrecioUnitario);

/// <summary>DTO de lectura de detalle de Venta.</summary>
public sealed record VentaDetalleDto(
    long Id,
    int ProductoId,
    string ProductoNombre,
    int? AlmacenId,
    decimal Cantidad,
    decimal Precio,
    decimal Importe);

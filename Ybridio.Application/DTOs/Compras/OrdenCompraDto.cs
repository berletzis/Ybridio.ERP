namespace Ybridio.Application.DTOs.Compras;

/// <summary>DTO de lectura para OrdenCompra.</summary>
public sealed record OrdenCompraDto(
    int Id,
    int EmpresaId,
    int ProveedorId,
    string ProveedorNombre,
    DateTime Fecha,
    decimal Total,
    int Estatus,
    string EstatusNombre);

/// <summary>DTO para crear una OrdenCompra.</summary>
public sealed record CrearOrdenCompraDto(
    int ProveedorId,
    DateTime Fecha,
    IReadOnlyList<OrdenCompraDetalleDto> Detalles);

/// <summary>DTO de detalle de OrdenCompra.</summary>
public sealed record OrdenCompraDetalleDto(
    int ProductoId,
    string ProductoNombre,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal);

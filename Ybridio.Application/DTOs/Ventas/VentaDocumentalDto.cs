using Ybridio.Domain.Ventas;

namespace Ybridio.Application.DTOs.Ventas;

/// <summary>DTO de resumen de venta documental para grids y listados.</summary>
public sealed record VentaDocumentalResumenDto(
    long          Id,
    int           EmpresaId,
    string        NombreCliente,
    EstatusVenta  Estatus,
    string        EstatusTexto,
    TipoPago      TipoPago,
    DateTime      Fecha,
    decimal       Total,
    decimal       TotalPagado,
    /// <summary>SaldoPendiente = Total - TotalPagado. Runtime, no almacenado.</summary>
    decimal       SaldoPendiente,
    long?         PedidoId,
    string?       Observaciones);

/// <summary>DTO completo de venta documental con detalles y pagos.</summary>
public sealed record VentaDocumentalDto(
    long                              Id,
    int                               EmpresaId,
    int                               SucursalId,
    int?                              RelacionComercialId,
    string                            NombreCliente,
    EstatusVenta                      Estatus,
    string                            EstatusTexto,
    TipoPago                          TipoPago,
    DateTime                          Fecha,
    decimal                           Subtotal,
    decimal                           Total,
    decimal                           TotalPagado,
    /// <summary>SaldoPendiente = Total - TotalPagado. Calculado runtime en el servicio/ViewModel.</summary>
    decimal                           SaldoPendiente,
    long?                             PedidoId,
    string?                           Observaciones,
    IReadOnlyList<DetalleLineaDto>    Detalles,
    IReadOnlyList<PagoVentaDto>       Pagos);

/// <summary>DTO para crear una venta documental nueva.</summary>
public sealed record CrearVentaDocumentalDto(
    int                                EmpresaId,
    int                                SucursalId,
    int?                               RelacionComercialId,
    string                             NombreCliente,
    TipoPago                           TipoPago,
    DateTime                           Fecha,
    long?                              PedidoId,
    string?                            Observaciones,
    IReadOnlyList<CrearDetalleLineaDto> Detalles);

/// <summary>DTO para actualizar el encabezado de una venta en Borrador.</summary>
public sealed record ActualizarVentaDocumentalDto(
    int?     RelacionComercialId,
    string   NombreCliente,
    TipoPago TipoPago,
    DateTime Fecha,
    string?  Observaciones);

/// <summary>DTO para registrar un pago (parcial o total) contra una venta.</summary>
public sealed record RegistrarPagoVentaDto(
    long    VentaId,
    decimal Monto,
    string  FormaPago,
    string? Referencia);

/// <summary>DTO de lectura de un pago registrado.</summary>
public sealed record PagoVentaDto(
    long     Id,
    long     VentaId,
    DateTime Fecha,
    decimal  Monto,
    string   FormaPago,
    string?  Referencia);

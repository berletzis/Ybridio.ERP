using Ybridio.Domain.Ventas;

namespace Ybridio.Application.DTOs.Ventas;

/// <summary>DTO de resumen de pedido para listas y grids.</summary>
public sealed record PedidoResumenDto(
    long          Id,
    int           EmpresaId,
    string        NombreCliente,
    long?         CotizacionId,
    EstatusPedido Estatus,
    string        EstatusTexto,
    DateTime      Fecha,
    DateTime?     FechaEntregaCompromiso,
    decimal       Total,
    string?       Observaciones);

/// <summary>DTO completo de pedido con detalles.</summary>
public sealed record PedidoDto(
    long                          Id,
    int                           EmpresaId,
    int?                          SucursalId,
    int?                          RelacionComercialId,
    string                        NombreCliente,
    long?                         CotizacionId,
    EstatusPedido                 Estatus,
    string                        EstatusTexto,
    DateTime                      Fecha,
    DateTime?                     FechaEntregaCompromiso,
    decimal                       Total,
    string?                       Observaciones,
    IReadOnlyList<DetalleLineaDto> Detalles);

/// <summary>DTO para actualizar el encabezado de un pedido existente.</summary>
public sealed record ActualizarPedidoDto(
    int?      RelacionComercialId,
    string    NombreCliente,
    DateTime  Fecha,
    DateTime? FechaEntregaCompromiso,
    string?   Observaciones);

/// <summary>DTO para crear un pedido nuevo.</summary>
public sealed record CrearPedidoDto(
    int                                EmpresaId,
    int?                               SucursalId,
    int?                               RelacionComercialId,
    string                             NombreCliente,
    long?                              CotizacionId,
    DateTime                           Fecha,
    DateTime?                          FechaEntregaCompromiso,
    string?                            Observaciones,
    IReadOnlyList<CrearDetalleLineaDto> Detalles);

using Ybridio.Domain.Ventas;

namespace Ybridio.Application.DTOs.Ventas;

/// <summary>DTO de resumen de pedido para listas y grids.</summary>
public sealed record PedidoResumenDto(
    long                    Id,
    int                     EmpresaId,
    string                  NombreCliente,
    long?                   CotizacionId,
    EstatusPedido           Estatus,
    string                  EstatusTexto,
    DateTime                Fecha,
    DateTime?               FechaEntregaCompromiso,
    decimal                 Total,
    string?                 Observaciones,
    /// <summary>Folio documental del pedido (ej: "PED-000001"). Null en registros anteriores a SerieDocumento.</summary>
    string?                 Folio = null,
    /// <summary>Folio de la cotización que originó este pedido (ej: "COT-000034"). Null si el pedido fue creado directamente.</summary>
    string?                 FolioCotizacionOrigen = null,
    /// <summary>Anticipo acumulado pagado contra este pedido.</summary>
    decimal                 AnticipoPagado = 0m,
    /// <summary>Estado financiero — independiente del workflow operacional.</summary>
    EstadoFinancieroPedido  EstadoFinanciero = EstadoFinancieroPedido.SinPago,
    string                  EstadoFinancieroTexto = "Sin Pago");

/// <summary>DTO completo de pedido con detalles, cargos y anticipos.</summary>
public sealed record PedidoDto(
    long                           Id,
    int                            EmpresaId,
    int?                           SucursalId,
    int?                           RelacionComercialId,
    string                         NombreCliente,
    long?                          CotizacionId,
    EstatusPedido                  Estatus,
    string                         EstatusTexto,
    DateTime                       Fecha,
    DateTime?                      FechaEntregaCompromiso,
    decimal                        Total,
    string?                        Observaciones,
    IReadOnlyList<DetalleLineaDto>  Detalles,
    /// <summary>Folio documental (ej: "PED-000001"). Null en registros anteriores a SerieDocumento.</summary>
    string?                        Folio    = null,
    /// <summary>Cargos accesorios del pedido — Commercial Charges Pattern.</summary>
    IReadOnlyList<PedidoCargoDto>? Cargos   = null,
    /// <summary>Subtotal = SUM(detalles.Importe neto). Null en registros legacy sin columna Subtotal.</summary>
    decimal?                       Subtotal = null,
    /// <summary>Monto mínimo de anticipo requerido. Null = sin requisito de anticipo.</summary>
    decimal?                       AnticipoRequerido = null,
    /// <summary>Anticipo acumulado pagado contra este pedido.</summary>
    decimal                        AnticipoPagado    = 0m,
    /// <summary>Estado financiero — independiente del workflow operacional.</summary>
    EstadoFinancieroPedido         EstadoFinanciero  = EstadoFinancieroPedido.SinPago,
    string                         EstadoFinancieroTexto = "Sin Pago",
    /// <summary>Historial de anticipos registrados contra este pedido.</summary>
    IReadOnlyList<AnticipoPedidoDto>? Anticipos = null,
    /// <summary>True si ya existe al menos una venta generada desde este pedido.</summary>
    bool    TieneVentaGenerada = false,
    /// <summary>Folio de la venta generada (ej: "VTA-000007") o "#id" si no tiene folio. Null si no hay venta.</summary>
    string? VentaGeneradaFolio = null);

/// <summary>DTO de lectura de un anticipo registrado contra un pedido.</summary>
public sealed record AnticipoPedidoDto(
    long     Id,
    long     PedidoId,
    DateTime Fecha,
    decimal  Monto,
    string   FormaPago,
    string?  Referencia,
    Guid     UsuarioRegistroId);

/// <summary>DTO para registrar un anticipo o pago parcial contra un pedido.</summary>
public sealed record RegistrarAnticipoDto(
    decimal  Monto,
    string   FormaPago,
    string?  Referencia,
    DateTime? Fecha = null);

/// <summary>DTO para establecer el anticipo requerido en un pedido.</summary>
public sealed record EstablecerAnticipoRequeridoDto(decimal? Monto);

/// <summary>DTO de lectura de un cargo accesorio de pedido.</summary>
public sealed record PedidoCargoDto(
    long    Id,
    string  Descripcion,
    decimal Importe,
    bool    AplicaIva,
    int     Orden);

/// <summary>DTO para agregar un cargo accesorio a un pedido.</summary>
public sealed record CrearPedidoCargoDto(
    string  Descripcion,
    decimal Importe,
    bool    AplicaIva,
    int     Orden = 0);

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

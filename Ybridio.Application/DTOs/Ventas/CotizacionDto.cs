using Ybridio.Domain.Ventas;

namespace Ybridio.Application.DTOs.Ventas;

/// <summary>
/// DTO de resumen de cotización para listas y grids.
/// Total incluido para permitir sumar rápidamente sin cargar detalles.
/// </summary>
public sealed record CotizacionResumenDto(
    long              Id,
    int               EmpresaId,
    string            NombreCliente,
    EstatusCotizacion Estatus,
    string            EstatusTexto,
    DateTime          Fecha,
    DateTime?         FechaVigencia,
    decimal           Total,
    string?           Observaciones);

/// <summary>DTO completo de cotización con detalles para vista de edición/detalle.</summary>
public sealed record CotizacionDto(
    long                          Id,
    int                           EmpresaId,
    int?                          SucursalId,
    int?                          RelacionComercialId,
    string                        NombreCliente,
    EstatusCotizacion             Estatus,
    string                        EstatusTexto,
    DateTime                      Fecha,
    DateTime?                     FechaVigencia,
    decimal                       Subtotal,
    decimal                       Total,
    string?                       Observaciones,
    IReadOnlyList<DetalleLineaDto> Detalles);

/// <summary>DTO para crear una cotización nueva.</summary>
public sealed record CrearCotizacionDto(
    int                                EmpresaId,
    int?                               SucursalId,
    int?                               RelacionComercialId,
    string                             NombreCliente,
    DateTime                           Fecha,
    DateTime?                          FechaVigencia,
    string?                            Observaciones,
    IReadOnlyList<CrearDetalleLineaDto> Detalles);

/// <summary>
/// Línea de detalle compartida para Cotizacion, Pedido y OrdenTrabajoMaterial.
/// Fórmula Importe: Cantidad × PrecioUnitario (calculado por el servicio al crear).
/// </summary>
public sealed record DetalleLineaDto(
    long    Id,
    int?    ProductoId,
    string  Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    /// <summary>Importe = Cantidad × PrecioUnitario. Persistido en BD.</summary>
    decimal Importe);

/// <summary>Input para crear una línea de detalle.</summary>
public sealed record CrearDetalleLineaDto(
    int?    ProductoId,
    string  Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario);

/// <summary>
/// DTO para actualizar el encabezado de una cotización existente (sin detalles).
/// Los detalles se gestionan individualmente via AgregarDetalle/EliminarDetalle.
/// </summary>
/// <remarks>
/// Arquitectura: separamos header de detalles para permitir edición incremental.
/// El header se actualiza en batch; los detalles se persisten inmediatamente al agregar/quitar.
/// </remarks>
public sealed record ActualizarCotizacionDto(
    int?      RelacionComercialId,
    string    NombreCliente,
    DateTime  Fecha,
    DateTime? FechaVigencia,
    string?   Observaciones);

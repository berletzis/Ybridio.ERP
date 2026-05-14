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
    string?           Observaciones,
    /// <summary>Folio documental (ej: "COT-000001"). Null en registros sin serie configurada.</summary>
    string?           Folio = null);

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
    IReadOnlyList<DetalleLineaDto> Detalles,
    /// <summary>Folio documental. Null en registros sin serie configurada.</summary>
    string?                        Folio   = null,
    /// <summary>Cargos accesorios (Flete, Maniobras, Seguro, etc.) de la cotización.</summary>
    IReadOnlyList<CotizacionCargoDto>? Cargos = null);

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
/// Importe neto = Cantidad × PrecioUnitario × (1 − DescuentoPct / 100).
/// </summary>
/// <remarks>ADR-042 — Commercial Discount Pattern.</remarks>
public sealed record DetalleLineaDto(
    long    Id,
    int?    ProductoId,
    string  Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    /// <summary>Importe neto después de descuento. Persistido en BD.</summary>
    decimal Importe,
    /// <summary>Porcentaje de descuento aplicado (0–100). 0 = sin descuento.</summary>
    decimal DescuentoPct   = 0m,
    /// <summary>SKU/código del producto. Null para ítems ad-hoc sin producto registrado.</summary>
    string? Sku            = null,
    /// <summary>Indica si esta línea aplica IVA. Heredado del Producto al crear; persiste para cálculo correcto.</summary>
    bool    IvaAplicable   = true);

/// <summary>
/// Input para crear una línea de detalle.
/// El importe neto lo calcula el servicio usando <see cref="CommercialDocumentCalculator"/>.
/// </summary>
/// <remarks>ADR-042 — Commercial Discount Pattern.</remarks>
public sealed record CrearDetalleLineaDto(
    int?    ProductoId,
    string  Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    /// <summary>Porcentaje de descuento (0–100). 0 = sin descuento.</summary>
    decimal DescuentoPct   = 0m,
    /// <summary>¿Esta línea aplica IVA? Heredar de Producto.IvaAplicable al agregar.</summary>
    bool    IvaAplicable   = true);

/// <summary>
/// DTO de lectura para un cargo accesorio de cotización (Commercial Charges Pattern).
/// OtroCargoId nullable: cargo puede ser libre sin referencia al catálogo.
/// </summary>
public sealed record CotizacionCargoDto(
    long    Id,
    int?    OtroCargoId,
    string  Descripcion,
    decimal Importe,
    bool    AplicaIva,
    int     Orden);

/// <summary>DTO para agregar un cargo accesorio a una cotización.</summary>
public sealed record CrearCotizacionCargoDto(
    int?    OtroCargoId,
    string  Descripcion,
    decimal Importe,
    bool    AplicaIva,
    int     Orden = 0);

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

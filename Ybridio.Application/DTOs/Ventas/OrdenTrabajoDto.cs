using Ybridio.Domain.Ventas;

namespace Ybridio.Application.DTOs.Ventas;

/// <summary>DTO de resumen de orden de trabajo para listas y grids.</summary>
public sealed record OTResumenDto(
    long               Id,
    int                EmpresaId,
    string             NombreCliente,
    EstatusOrdenTrabajo Estatus,
    string             EstatusTexto,
    DateTime           Fecha,
    DateTime?          FechaCompromiso,
    string             Descripcion,
    decimal            Total,
    bool               EsUrgente);

/// <summary>DTO completo de orden de trabajo con materiales.</summary>
public sealed record OrdenTrabajoDto(
    long                          Id,
    int                           EmpresaId,
    int?                          SucursalId,
    int?                          ClienteId,
    string                        NombreCliente,
    long?                         PedidoId,
    EstatusOrdenTrabajo           Estatus,
    string                        EstatusTexto,
    DateTime                      Fecha,
    DateTime?                     FechaCompromiso,
    string                        Descripcion,
    string?                       Observaciones,
    Guid?                         ResponsableId,
    /// <summary>
    /// Total de la OT.
    /// Fórmula: SUM(materiales.Importe). Persistido en BD; recalculado al agregar/quitar materiales.
    /// </summary>
    decimal                       Total,
    IReadOnlyList<DetalleLineaDto> Materiales);

/// <summary>DTO para crear una orden de trabajo nueva.</summary>
public sealed record CrearOrdenTrabajoDto(
    int      EmpresaId,
    int?     SucursalId,
    int?     ClienteId,
    string   NombreCliente,
    long?    PedidoId,
    DateTime Fecha,
    DateTime? FechaCompromiso,
    string   Descripcion,
    string?  Observaciones,
    Guid?    ResponsableId);

/// <summary>DTO para actualizar el encabezado de una OT existente (sin materiales).</summary>
public sealed record ActualizarOrdenTrabajoDto(
    int?      ClienteId,
    string    NombreCliente,
    DateTime  Fecha,
    DateTime? FechaCompromiso,
    string    Descripcion,
    string?   Observaciones,
    Guid?     ResponsableId);

/// <summary>DTO para agregar un material a una OT.</summary>
public sealed record AgregarOTMaterialDto(
    int?    ProductoId,
    string  Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario);

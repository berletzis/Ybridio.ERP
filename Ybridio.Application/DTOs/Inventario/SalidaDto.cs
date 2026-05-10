namespace Ybridio.Application.DTOs.Inventario;

/// <summary>
/// DTO de resumen para la lista de salidas de inventario.
/// TieneAutorizacion = true cuando <see cref="Ybridio.Domain.Inventario.Salida.UsuarioAutorizacionId"/> está asignado.
/// VentaId presente cuando la salida fue originada por una venta confirmada.
/// </summary>
public sealed record SalidaResumenDto(
    long     Id,
    int      EmpresaId,
    int      SucursalId,
    int      AlmacenId,
    string   AlmacenNombre,
    string?  Folio,
    DateTime Fecha,
    string   ConceptoNombre,
    string   EstatusNombre,
    int      CantidadDetalles,
    decimal  Total,
    bool     Aplicada,
    bool     TieneAutorizacion,
    string?  Observaciones,
    /// <summary>ID de la venta que originó la salida, si aplica.</summary>
    long?    VentaId          = null,
    /// <summary>Nombre del usuario que aplicó la salida.</summary>
    string?  UsuarioNombre    = null);

namespace Ybridio.Application.DTOs.Inventario;

/// <summary>
/// DTO de resumen para la lista de salidas de inventario.
/// TieneAutorizacion = true cuando <see cref="Ybridio.Domain.Inventario.Salida.UsuarioAutorizacionId"/> está asignado.
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
    string?  Observaciones);

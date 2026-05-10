namespace Ybridio.Application.DTOs.Inventario;

/// <summary>
/// DTO de resumen para la lista de entradas de inventario.
/// Incluye EmpresaId y SucursalId para que la UI pueda validar scopes sin queries adicionales.
/// ProveedorNombre presente cuando la entrada fue originada por una compra/proveedor.
/// </summary>
public sealed record EntradaResumenDto(
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
    string?  Observaciones,
    /// <summary>Nombre del proveedor cuando la entrada es por compra.</summary>
    string?  ProveedorNombre  = null,
    /// <summary>Nombre del usuario que aplicó la entrada.</summary>
    string?  UsuarioNombre    = null);

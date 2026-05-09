namespace Ybridio.Application.DTOs.Inventario;

/// <summary>
/// DTO de resumen para la lista de entradas de inventario.
/// Incluye EmpresaId y SucursalId para que la UI pueda validar scopes sin queries adicionales.
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
    string?  Observaciones);

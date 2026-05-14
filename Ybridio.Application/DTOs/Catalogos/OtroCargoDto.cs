namespace Ybridio.Application.DTOs.Catalogos;

/// <summary>
/// Cargo accesorio documental (Flete, Maniobras, Seguro, etc.).
/// Uso: catálogo editable en la sección Otros Cargos del módulo Configuración;
/// selector en documentos comerciales (cotizaciones, pedidos, ventas).
/// </summary>
public sealed record OtroCargoDto(
    int     Id,
    string  Codigo,
    string  Nombre,
    string  TipoCargo,
    bool    AplicaIva,
    int?    TipoImpuestoId,
    string? TipoImpuestoNombre,
    int     OrdenVisual,
    bool    Activo
);

/// <summary>DTO para crear o actualizar un cargo accesorio.</summary>
public sealed record GuardarOtroCargoDto(
    int     Id,
    string  Codigo,
    string  Nombre,
    string  TipoCargo,
    bool    AplicaIva,
    int?    TipoImpuestoId,
    int     OrdenVisual,
    bool    Activo
);

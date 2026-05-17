namespace Ybridio.Application.DTOs.Inventario;

/// <summary>DTO de almacén para listados y resolución de almacén principal de sucursal.</summary>
public sealed record AlmacenDto(
    int     Id,
    int     EmpresaId,
    int     SucursalId,
    string? Codigo,
    string  Nombre,
    string? Descripcion,
    bool    Activo,
    bool    EsPrincipal);

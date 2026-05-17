namespace Ybridio.Application.DTOs.Inventario;

/// <summary>DTO para crear un nuevo almacén en la sucursal indicada.</summary>
public sealed record CrearAlmacenDto(
    int     EmpresaId,
    int     SucursalId,
    string  Nombre,
    string? Codigo,
    string? Descripcion);

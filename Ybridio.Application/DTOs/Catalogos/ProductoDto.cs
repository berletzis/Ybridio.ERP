namespace Ybridio.Application.DTOs.Catalogos;

/// <summary>DTO de lectura para Producto.</summary>
public sealed record ProductoDto(
    int Id,
    int EmpresaId,
    string Codigo,
    string Nombre,
    decimal Precio,
    bool Activo);

/// <summary>DTO para crear o actualizar un Producto.</summary>
public sealed record UpsertProductoDto(
    string Codigo,
    string Nombre,
    decimal Precio,
    bool Activo = true);

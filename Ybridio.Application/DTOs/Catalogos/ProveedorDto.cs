namespace Ybridio.Application.DTOs.Catalogos;

/// <summary>DTO de lectura para Proveedor.</summary>
public sealed record ProveedorDto(
    int Id,
    int EmpresaId,
    string Nombre,
    string? RFC,
    string? Email,
    string? Telefono,
    bool Activo);

/// <summary>DTO para crear o actualizar un Proveedor.</summary>
public sealed record UpsertProveedorDto(
    string Nombre,
    string? RFC,
    string? Email,
    string? Telefono,
    bool Activo = true);

namespace Ybridio.Application.DTOs.Catalogos;

/// <summary>DTO de lectura para Cliente.</summary>
public sealed record ClienteDto(
    int Id,
    int EmpresaId,
    string Nombre,
    string? RFC,
    string? Email,
    string? Telefono,
    bool Activo);

/// <summary>DTO para crear o actualizar un Cliente.</summary>
public sealed record UpsertClienteDto(
    string Nombre,
    string? RFC,
    string? Email,
    string? Telefono,
    bool Activo = true);

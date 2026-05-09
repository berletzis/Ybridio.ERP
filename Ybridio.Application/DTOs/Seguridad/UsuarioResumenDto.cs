namespace Ybridio.Application.DTOs.Seguridad;

/// <summary>
/// DTO enriquecido para la vista administrativa de usuarios.
/// Incluye roles y perfiles asignados como textos concatenados para facilitar la visualización en grids.
/// </summary>
public sealed record UsuarioResumenDto(
    Guid    Id,
    int     EmpresaId,
    string  Nombre,
    string  UserName,
    string? Email,
    bool    Activo,
    string  RolesTexto,
    string  PerfilesTexto);

namespace Ybridio.Application.DTOs.Seguridad;

/// <summary>
/// DTO enriquecido para la vista administrativa de roles.
/// Incluye conteos de permisos y usuarios asignados.
/// </summary>
public sealed record RolAdminDto(
    Guid   Id,
    string Nombre,
    int    CantidadPermisos,
    int    CantidadUsuarios);

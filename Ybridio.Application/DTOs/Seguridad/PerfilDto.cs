namespace Ybridio.Application.DTOs.Seguridad;

/// <summary>DTO de lectura para un perfil de permisos.</summary>
public sealed record PerfilDto(
    int     Id,
    string  Nombre,
    string? Descripcion,
    bool    Activo,
    int     CantidadPermisos);

/// <summary>DTO para crear un nuevo perfil con sus permisos iniciales.</summary>
public sealed record CrearPerfilDto(
    string               Nombre,
    string?              Descripcion,
    Guid                 UsuarioCreacionId,
    IReadOnlyList<int>   PermisoIds);

/// <summary>DTO para actualizar el nombre/descripción/estado de un perfil.</summary>
public sealed record ActualizarPerfilDto(
    string  Nombre,
    string? Descripcion,
    bool    Activo,
    Guid    UsuarioModificacionId);

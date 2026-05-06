namespace Ybridio.Application.DTOs.Seguridad;

/// <summary>DTO de credenciales para login.</summary>
public sealed record LoginDto(string Email, string Password);

/// <summary>DTO de lectura para un usuario del sistema.</summary>
public sealed record UsuarioDto(
    Guid Id,
    int EmpresaId,
    string Nombre,
    string UserName,
    string? Email,
    bool Activo);

/// <summary>DTO para crear un usuario.</summary>
public sealed record CrearUsuarioDto(
    int EmpresaId,
    string Nombre,
    string UserName,
    string Email,
    string Password);

/// <summary>DTO para actualizar datos básicos de un usuario.</summary>
public sealed record ActualizarUsuarioDto(string Nombre, string? Email, bool Activo);

/// <summary>DTO de lectura para un rol.</summary>
public sealed record RolDto(
    Guid Id,
    string Name,
    DateTime FechaCreacion);

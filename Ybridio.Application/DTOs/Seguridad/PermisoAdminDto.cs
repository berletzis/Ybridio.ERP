namespace Ybridio.Application.DTOs.Seguridad;

/// <summary>
/// DTO de lectura para un permiso del sistema, incluyendo el módulo al que pertenece.
/// Usado en vistas administrativas de solo lectura.
/// </summary>
public sealed record PermisoAdminDto(
    int    Id,
    string Clave,
    string ModuloNombre,
    string ModuloClave,
    string Nombre);

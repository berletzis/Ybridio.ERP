namespace Ybridio.Application.DTOs.Catalogos;

/// <summary>
/// Parámetro de configuración global para la empresa en sesión.
/// Uso: visualización y edición en la sección Parámetros del módulo Configuración.
/// </summary>
public sealed record ParametroGlobalDto(
    int     Id,
    string  Clave,
    string  Valor,
    string? Descripcion,
    string  TipoDato,
    string  Grupo,
    int     OrdenVisual,
    bool    Activo
);

/// <summary>DTO para crear o actualizar un parámetro global.</summary>
public sealed record GuardarParametroGlobalDto(
    int     Id,
    string  Clave,
    string  Valor,
    string? Descripcion,
    string  TipoDato,
    string  Grupo,
    int     OrdenVisual,
    bool    Activo
);

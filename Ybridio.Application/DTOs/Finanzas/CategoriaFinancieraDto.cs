namespace Ybridio.Application.DTOs.Finanzas;

/// <summary>DTO de lectura para categoría financiera.</summary>
public sealed record CategoriaFinancieraDto(
    int     Id,
    int     EmpresaId,
    string  TipoAplicable,
    string  Nombre,
    string? Descripcion,
    string? Color,
    bool    Activo);

/// <summary>DTO para crear o actualizar una categoría financiera.</summary>
public sealed record GuardarCategoriaFinancieraDto(
    int     EmpresaId,
    string  TipoAplicable,
    string  Nombre,
    string? Descripcion,
    string? Color);

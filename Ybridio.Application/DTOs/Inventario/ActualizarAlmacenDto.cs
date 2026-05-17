namespace Ybridio.Application.DTOs.Inventario;

/// <summary>DTO para actualizar nombre, código y descripción de un almacén existente.</summary>
public sealed record ActualizarAlmacenDto(
    string  Nombre,
    string? Codigo,
    string? Descripcion);

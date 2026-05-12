using Ybridio.Domain.Catalogos;

namespace Ybridio.Application.DTOs.Directorio;

/// <summary>DTO de lectura de una Persona del directorio.</summary>
public sealed record PersonaDto(
    int      Id,
    int      EmpresaId,
    int?     EmpresaComercialId,
    string   Nombre,
    string?  Apellidos,
    string   NombreCompleto,
    string?  RFC,
    string?  Email,
    string?  Telefono,
    string?  Direccion,
    string?  Notas,
    bool     Activo);

/// <summary>DTO para crear una Persona.</summary>
public sealed record CrearPersonaDto(
    int      EmpresaId,
    int?     EmpresaComercialId,
    string   Nombre,
    string?  Apellidos,
    string?  RFC,
    string?  Email,
    string?  Telefono,
    string?  Direccion,
    string?  Notas);

/// <summary>DTO para actualizar una Persona existente.</summary>
public sealed record ActualizarPersonaDto(
    int?     EmpresaComercialId,
    string   Nombre,
    string?  Apellidos,
    string?  RFC,
    string?  Email,
    string?  Telefono,
    string?  Direccion,
    string?  Notas,
    bool     Activo);

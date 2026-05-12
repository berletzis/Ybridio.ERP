namespace Ybridio.Application.DTOs.Directorio;

/// <summary>DTO de lectura de una EmpresaComercial del directorio.</summary>
public sealed record EmpresaComercialDto(
    int      Id,
    int      EmpresaId,
    string   RazonSocial,
    string?  NombreComercial,
    string?  RFC,
    string?  Email,
    string?  Telefono,
    string?  Direccion,
    string?  Notas,
    bool     Activo);

/// <summary>DTO para crear una EmpresaComercial.</summary>
public sealed record CrearEmpresaComercialDto(
    int      EmpresaId,
    string   RazonSocial,
    string?  NombreComercial,
    string?  RFC,
    string?  Email,
    string?  Telefono,
    string?  Direccion,
    string?  Notas);

/// <summary>DTO para actualizar una EmpresaComercial existente.</summary>
public sealed record ActualizarEmpresaComercialDto(
    string   RazonSocial,
    string?  NombreComercial,
    string?  RFC,
    string?  Email,
    string?  Telefono,
    string?  Direccion,
    string?  Notas,
    bool     Activo);

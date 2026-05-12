using Ybridio.Domain.Catalogos;

namespace Ybridio.Application.DTOs.Directorio;

/// <summary>
/// DTO de lectura de una RelacionComercial.
/// Incluye el nombre resuelto del socio (persona o empresa comercial).
/// </summary>
public sealed record RelacionComercialDto(
    int                      Id,
    int                      EmpresaId,
    int?                     PersonaId,
    int?                     EmpresaComercialId,
    TipoRelacionComercial    TipoRelacion,
    string                   TipoRelacionDisplay,
    decimal                  LimiteCredito,
    bool                     Activo,
    string?                  Observaciones,
    /// <summary>Nombre resuelto del socio: NombreCompleto de Persona o RazonSocial de EmpresaComercial.</summary>
    string                   NombreSocio,
    /// <summary>Etiqueta de tipo de socio para UX: "Persona Física" o "Empresa".</summary>
    string                   TipoSocio);

/// <summary>DTO para crear una RelacionComercial vinculada a una Persona.</summary>
public sealed record CrearRelacionComercialPersonaDto(
    int                      EmpresaId,
    int                      PersonaId,
    TipoRelacionComercial    TipoRelacion,
    decimal                  LimiteCredito,
    string?                  Observaciones);

/// <summary>DTO para crear una RelacionComercial vinculada a una EmpresaComercial.</summary>
public sealed record CrearRelacionComercialEmpresaDto(
    int                      EmpresaId,
    int                      EmpresaComercialId,
    TipoRelacionComercial    TipoRelacion,
    decimal                  LimiteCredito,
    string?                  Observaciones);

/// <summary>DTO para actualizar una RelacionComercial existente.</summary>
public sealed record ActualizarRelacionComercialDto(
    TipoRelacionComercial    TipoRelacion,
    decimal                  LimiteCredito,
    bool                     Activo,
    string?                  Observaciones);

/// <summary>
/// DTO compacto para selectores de cotizacion/venta.
/// Muestra nombre del socio + tipo visual para UX.
/// </summary>
public sealed class RelacionComercialSelectorDto
{
    /// <summary>Identificador de la relación comercial.</summary>
    public int    Id                { get; init; }
    /// <summary>Nombre resuelto: NombreCompleto (persona) o RazonSocial (empresa).</summary>
    public string NombreSocio       { get; init; } = string.Empty;
    /// <summary>"Persona Física" o "Empresa".</summary>
    public string TipoSocio         { get; init; } = string.Empty;
    /// <summary>Etiqueta localizable del tipo de relación comercial.</summary>
    public string TipoRelacionDisplay { get; init; } = string.Empty;
    /// <summary>Información secundaria: RFC · email · teléfono. Calculado para UX.</summary>
    public string InfoSecundaria    { get; init; } = string.Empty;
    /// <summary>
    /// Glyph de icono semántico para WinUI según tipo de socio.
    /// Persona Física: E77B (Contact); Empresa: E909 (Work); default: E716 (People).
    /// </summary>
    public string GlyphForTipoSocio => TipoSocio switch
    {
        "Empresa"       => "\uE909",
        "Persona Física" => "\uE77B",
        _               => "\uE716"
    };
}

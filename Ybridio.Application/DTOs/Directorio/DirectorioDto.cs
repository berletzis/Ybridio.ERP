namespace Ybridio.Application.DTOs.Directorio;

/// <summary>
/// DTO para selector institucional del Directorio comercial (ADR-038).
/// Representa una entidad real del Directorio: Persona o EmpresaComercial.
/// NO representa una RelacionComercial — ésta se crea/reutiliza bajo demanda al guardar.
/// </summary>
/// <remarks>
/// Regla institucional ADR-038:
/// El Directorio (Persona / EmpresaComercial) es el source of truth para búsqueda UI.
/// RelacionComercial es un vínculo operativo creado únicamente cuando existe transacción real.
/// </remarks>
public sealed class DirectorioSelectorDto
{
    /// <summary>
    /// Tipo de entidad del directorio.
    /// </summary>
    public DirectorioEntityType EntityType { get; init; }

    /// <summary>ID de la Persona si EntityType == Persona. Null si es Empresa.</summary>
    public int? PersonaId { get; init; }

    /// <summary>ID de la EmpresaComercial si EntityType == Empresa. Null si es Persona.</summary>
    public int? EmpresaComercialId { get; init; }

    /// <summary>Nombre para mostrar: NombreCompleto (persona) o NombreComercial/RazonSocial (empresa).</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>RFC fiscal. Puede ser null.</summary>
    public string? RFC { get; init; }

    /// <summary>Email de contacto. Puede ser null.</summary>
    public string? Email { get; init; }

    /// <summary>Teléfono de contacto. Puede ser null.</summary>
    public string? Telefono { get; init; }

    /// <summary>
    /// Texto visual de tipo para el badge UX.
    /// "Persona Física" o "Empresa".
    /// </summary>
    public string TipoVisual => EntityType switch
    {
        DirectorioEntityType.Empresa => "Empresa",
        _                            => "Persona Física",
    };

    /// <summary>
    /// Glyph semántico WinUI Segoe Fluent Icons según tipo de entidad.
    /// Empresa: E731 (Building); Persona: E77B (Contact).
    /// </summary>
    public string Glyph => EntityType switch
    {
        DirectorioEntityType.Empresa => "\uE731",
        _                            => "\uE77B",
    };

    /// <summary>
    /// Información secundaria para preview UX: tipo · RFC · email · teléfono.
    /// </summary>
    public string InfoSecundaria
    {
        get
        {
            var partes = new System.Collections.Generic.List<string> { TipoVisual };
            if (RFC      is { Length: > 0 }) partes.Add(RFC);
            if (Email    is { Length: > 0 }) partes.Add(Email);
            if (Telefono is { Length: > 0 }) partes.Add(Telefono);
            return string.Join(" · ", partes);
        }
    }
}

/// <summary>
/// Discriminador de tipo de entidad del Directorio.
/// </summary>
public enum DirectorioEntityType
{
    /// <summary>Persona física o contacto humano (core.Persona).</summary>
    Persona = 1,

    /// <summary>Empresa comercial / persona moral (core.EmpresaComercial).</summary>
    Empresa = 2,
}

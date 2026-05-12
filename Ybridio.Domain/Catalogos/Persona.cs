using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

/// <summary>
/// Persona física o contacto humano registrado en el directorio comercial.
/// Puede estar asociada opcionalmente a una EmpresaComercial como representante o contacto.
/// </summary>
public class Persona : AuditableEntity
{
    public int     Id          { get; set; }

    /// <summary>Empresa multi-tenant dueña del registro.</summary>
    public int     EmpresaId   { get; set; }

    /// <summary>EmpresaComercial a la que pertenece esta persona como contacto (opcional).</summary>
    public int?    EmpresaComercialId { get; set; }

    public string  Nombre      { get; set; } = string.Empty;
    public string? Apellidos   { get; set; }

    /// <summary>Nombre completo derivado. Se recalcula al guardar; no se persiste de forma independiente.</summary>
    public string NombreCompleto => string.IsNullOrWhiteSpace(Apellidos)
        ? Nombre
        : $"{Nombre} {Apellidos}";

    public string? RFC        { get; set; }
    public string? Email      { get; set; }
    public string? Telefono   { get; set; }
    public string? Direccion  { get; set; }
    public string? Notas      { get; set; }
    public bool    Activo     { get; set; } = true;

    // Navegación
    public Empresa          Empresa          { get; set; } = null!;
    public EmpresaComercial? EmpresaComercial { get; set; }
}

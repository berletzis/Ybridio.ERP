using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

/// <summary>
/// Entidad moral o jurídica registrada en el directorio comercial.
/// Distinta de <see cref="Empresa"/> que es la entidad multi-tenant propietaria del ERP.
/// </summary>
public class EmpresaComercial : AuditableEntity
{
    public int     Id               { get; set; }

    /// <summary>Empresa multi-tenant dueña del registro.</summary>
    public int     EmpresaId        { get; set; }

    public string  RazonSocial      { get; set; } = string.Empty;
    public string? NombreComercial  { get; set; }
    public string? RFC              { get; set; }
    public string? Email            { get; set; }
    public string? Telefono         { get; set; }
    public string? Direccion        { get; set; }
    public string? Notas            { get; set; }
    public bool    Activo           { get; set; } = true;

    // Navegación
    public Empresa              Empresa   { get; set; } = null!;
    public ICollection<Persona> Contactos { get; set; } = [];
}

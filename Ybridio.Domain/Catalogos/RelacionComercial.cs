using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

/// <summary>
/// Vínculo comercial ERP entre la empresa multi-tenant y un socio (Persona o EmpresaComercial).
/// Reemplaza conceptualmente a la entidad <c>Cliente</c> ofreciendo un modelo más completo.
/// </summary>
/// <remarks>
/// REGLA INVARIANTE: exactamente uno de <see cref="PersonaId"/> o <see cref="EmpresaComercialId"/> debe tener valor.
/// NO se duplican datos de la persona/empresa — se navega a través de las propiedades de navegación.
/// LimiteCredito = 0 significa contado; mayor que 0 es crédito habilitado.
/// SaldoPendiente no se persiste — se calcula en runtime desde CuentaPorCobrar.
/// </remarks>
public class RelacionComercial : AuditableEntity
{
    public int  Id        { get; set; }

    /// <summary>Empresa multi-tenant dueña del registro.</summary>
    public int  EmpresaId { get; set; }

    /// <summary>Persona física vinculada. Exclusivo con <see cref="EmpresaComercialId"/>.</summary>
    public int? PersonaId          { get; set; }

    /// <summary>Empresa jurídica vinculada. Exclusivo con <see cref="PersonaId"/>.</summary>
    public int? EmpresaComercialId { get; set; }

    /// <summary>Rol comercial del socio con la empresa multi-tenant.</summary>
    public TipoRelacionComercial TipoRelacion { get; set; } = TipoRelacionComercial.Prospecto;

    /// <summary>
    /// Límite de crédito en moneda local.
    /// 0 = sin crédito (contado únicamente).
    /// </summary>
    public decimal LimiteCredito  { get; set; }

    public bool    Activo         { get; set; } = true;
    public string? Observaciones  { get; set; }

    // Navegación
    public Empresa           Empresa           { get; set; } = null!;
    public Persona?          Persona           { get; set; }
    public EmpresaComercial? EmpresaComercial  { get; set; }
}

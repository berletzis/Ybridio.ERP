// ── Ybridio.Domain/Catalogos/TipoProducto.cs ─────────────────────────────────
using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

/// <summary>
/// Clasificación comercial de productos: Producto Físico, Servicio, Consumible, Equipo, Licencia, etc.
/// Los Servicios NO tienen tabla propia — se registran como Producto con TipoProducto = "SERV".
/// </summary>
/// <remarks>
/// Product Type Classification Pattern:
/// La Clave es el identificador operacional humano (PROD, SERV, REF, EQP, LIC, MOB).
/// El Id es técnico — nunca usar como clave operacional en reglas de negocio.
/// </remarks>
public class TipoProducto : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }

    /// <summary>
    /// Clave operacional corta. Ej: "PROD", "SERV", "REF", "EQP", "LIC", "MOB".
    /// Debe ser único por empresa. El operador la usa para identificar rápidamente el tipo.
    /// </summary>
    public string Clave { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Orden de presentación en selectores y catálogos.</summary>
    public int OrdenVisual { get; set; } = 0;

    public bool Activo { get; set; } = true;

    // Navegación
    public Empresa Empresa { get; set; } = null!;
}
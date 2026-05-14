using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

/// <summary>
/// Parámetro de configuración global del ERP.
/// Almacena pares Clave-Valor que definen el comportamiento operacional del sistema
/// para una empresa dada. No es un catálogo transaccional; es la base operacional de configuración.
/// </summary>
public class ParametroGlobal : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }

    /// <summary>Clave única del parámetro dentro de la empresa. Ej: "iva.tasa.default", "moneda.codigo.default".</summary>
    public string Clave { get; set; } = string.Empty;

    /// <summary>Valor del parámetro serializado como texto. El tipo semántico lo indica TipoDato.</summary>
    public string Valor { get; set; } = string.Empty;

    /// <summary>Descripción legible del parámetro para usuarios administradores.</summary>
    public string? Descripcion { get; set; }

    /// <summary>Tipo semántico del valor: "decimal", "int", "string", "bool".</summary>
    public string TipoDato { get; set; } = "string";

    /// <summary>Categoría de agrupación visual. Ej: "Fiscal", "Moneda", "Documentos", "Inventario".</summary>
    public string Grupo { get; set; } = "General";

    /// <summary>Orden de presentación dentro del grupo.</summary>
    public int OrdenVisual { get; set; } = 0;

    public bool Activo { get; set; } = true;

    // Navegación
    public Empresa Empresa { get; set; } = null!;
}

using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

/// <summary>
/// Cargo accesorio documental que puede aplicarse a documentos comerciales (cotizaciones, pedidos, ventas).
/// NO es un producto inventariable ni un servicio. Representa cargos documentales como
/// Flete, Maniobras, Seguro, Empaque, Comisión bancaria, Recargo urgente.
/// </summary>
/// <remarks>
/// DIFERENCIA CONCEPTUAL: Servicios (Instalación, Consultoría) → usan entidad Producto.
/// OtroCargo → cargo documental accesorio, sin inventario, sin kardex.
/// </remarks>
public class OtroCargo : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }

    /// <summary>Código corto del cargo. Ej: "FLT", "MAN", "SEG".</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Nombre descriptivo del cargo. Ej: "Flete Nacional", "Maniobras de carga".</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Categoría del tipo de cargo: "Logística", "Financiero", "Seguro", "Operativo", "Otro".</summary>
    public string TipoCargo { get; set; } = "Otro";

    /// <summary>Indica si este cargo aplica IVA al calcularse en un documento.</summary>
    public bool AplicaIva { get; set; } = false;

    /// <summary>Tipo de impuesto específico si aplica (puede diferir del IVA default).</summary>
    public int? TipoImpuestoId { get; set; }

    /// <summary>Orden de presentación en selectores documentales.</summary>
    public int OrdenVisual { get; set; } = 0;

    public bool Activo { get; set; } = true;

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public TipoImpuesto? TipoImpuesto { get; set; }
}

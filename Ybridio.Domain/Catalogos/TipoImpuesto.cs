using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

/// <summary>
/// Catálogo fiscal institucional del ERP.
/// Define los tipos de impuesto disponibles por empresa: IVA 16%, IVA 8%, Exento, IEPS, etc.
/// Es la ÚNICA fuente de verdad fiscal — no duplicar tasas en ParametroGlobal.
/// </summary>
/// <remarks>
/// Commercial Tax Pattern (Single Source of Truth Fiscal Rule):
/// - TipoImpuesto = QUÉ impuestos existen y cuál es su tasa.
/// - ParametroGlobal (impuesto.default.X) = CUÁL TipoImpuesto usar por default en cada contexto.
/// - CommercialDocumentCalculator recibe la tasa como parámetro desde IConfiguracionFiscalService.
/// - NUNCA hardcodear 0.16 — usar Porcentaje / 100 del TipoImpuesto cargado.
/// </remarks>
public class TipoImpuesto : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }

    /// <summary>Nombre legible del impuesto. Ej: "IVA 16%", "IVA Frontera 8%", "Exento".</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Código corto único del impuesto. Ej: "IVA16", "IVA8", "EXENTO", "IEPS".
    /// Útil para búsqueda programática y mapeo con catálogos SAT.
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>
    /// Porcentaje del impuesto (0–100). Ej: 16.00 para IVA 16%, 0.00 para Exento.
    /// Al calcular: tasa = Porcentaje / 100m.
    /// </summary>
    public decimal Porcentaje { get; set; }

    /// <summary>Tipo de gravamen fiscal que clasifica la naturaleza del impuesto.</summary>
    public TipoGravamen TipoGravamen { get; set; } = TipoGravamen.IVA;

    /// <summary>
    /// True si la operación es exenta de impuesto (TipoGravamen = Exento AND Porcentaje = 0).
    /// Almacenado explícitamente para facilitar queries y filtros en documentos.
    /// </summary>
    public bool EsExento { get; set; } = false;

    /// <summary>Descripción técnica o legal del impuesto para referencia administrativa.</summary>
    public string? Descripcion { get; set; }

    /// <summary>Orden de presentación en selectores y catálogos. Menor número = aparece primero.</summary>
    public int OrdenVisual { get; set; } = 0;

    public bool Activo { get; set; } = true;

    // Navegación
    public Empresa Empresa { get; set; } = null!;
}

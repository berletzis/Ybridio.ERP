using Ybridio.Domain.Catalogos;

namespace Ybridio.Domain.Ventas;

/// <summary>
/// Cargo accesorio de una cotización (Flete, Maniobras, Seguro, Empaque, etc.).
/// Commercial Charges Pattern: los OtrosCargos NO son productos — se registran
/// en sección separada del documento, con impacto propio en impuestos y total.
/// </summary>
/// <remarks>
/// Entidad simple (sin AuditableEntity) — es un child record de Cotizacion.
/// La trazabilidad de auditoría se hereda del documento padre.
/// OtroCargoId es opcional: el usuario puede ingresar un cargo libre sin referencia al catálogo.
/// </remarks>
public class CotizacionCargo
{
    public long    Id           { get; set; }
    public long    CotizacionId { get; set; }

    /// <summary>Referencia al catálogo OtroCargo. Null si el cargo es de texto libre.</summary>
    public int?    OtroCargoId  { get; set; }

    /// <summary>Descripción del cargo. Pre-llenada desde OtroCargo.Nombre o ingresada libremente.</summary>
    public string  Descripcion  { get; set; } = string.Empty;

    /// <summary>Monto total del cargo (no tiene Cantidad × Precio — es un importe directo).</summary>
    public decimal Importe      { get; set; }

    /// <summary>True si este cargo aplica IVA. Heredado de OtroCargo.AplicaIva o configurado por el usuario.</summary>
    public bool    AplicaIva    { get; set; } = false;

    /// <summary>Orden de presentación dentro de la sección Otros Cargos del documento.</summary>
    public int     Orden        { get; set; } = 0;

    // Navegación
    public Cotizacion Cotizacion { get; set; } = null!;
    public OtroCargo? OtroCargo  { get; set; }
}

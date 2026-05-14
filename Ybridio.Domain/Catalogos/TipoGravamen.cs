namespace Ybridio.Domain.Catalogos;

/// <summary>
/// Tipo de gravamen fiscal que define la naturaleza del impuesto.
/// Determina cómo se aplica el impuesto en documentos comerciales y reportes fiscales.
/// </summary>
/// <remarks>
/// Commercial Tax Pattern (Single Source of Truth Fiscal Rule):
/// El TipoGravamen es el clasificador institucional del impuesto.
/// La tasa efectiva vive en TipoImpuesto.Porcentaje.
/// </remarks>
public enum TipoGravamen
{
    /// <summary>Impuesto al Valor Agregado (IVA): 16%, 8%, etc.</summary>
    IVA = 1,

    /// <summary>Impuesto Especial sobre Producción y Servicios (IEPS).</summary>
    IEPS = 2,

    /// <summary>Impuesto Sobre la Renta — Retención aplicada al receptor.</summary>
    ISRRetencion = 3,

    /// <summary>Operación exenta de impuesto. Porcentaje = 0.</summary>
    Exento = 4,

    /// <summary>Otro tipo de gravamen no clasificado en las categorías anteriores.</summary>
    Otro = 5,
}

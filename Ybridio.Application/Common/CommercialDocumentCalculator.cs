using System.Collections.Generic;
using System.Linq;
using Ybridio.Domain.Common;

namespace Ybridio.Application.Common;

/// <summary>
/// Calculador institucional reutilizable para documentos comerciales del ERP.
/// Single Source of Truth para toda la aritmética de líneas, descuentos e impuestos.
/// </summary>
/// <remarks>
/// ADR-042 — Commercial Discount Pattern:
/// - Importe neto = Cantidad × PrecioUnitario × (1 − DescuentoPct / 100)
/// - Descuento se aplica ANTES del IVA (el IVA se calcula sobre el importe neto)
/// - Descuento global NO acumulable con descuentos individuales por línea
/// - Reutilizable en: Cotizaciones, Pedidos, Órdenes de Compra, Ventas, Facturación
/// </remarks>
public static class CommercialDocumentCalculator
{
    /// <summary>
    /// Calcula el importe neto de una línea: Cantidad × PrecioUnitario × (1 − DescuentoPct / 100).
    /// El descuento se aplica sobre el bruto; el IVA se calcula sobre este resultado.
    /// </summary>
    /// <param name="cantidad">Cantidad de unidades.</param>
    /// <param name="precioUnitario">Precio unitario antes de descuento.</param>
    /// <param name="descuentoPct">Porcentaje de descuento (0–100). 0 = sin descuento.</param>
    /// <returns>Importe neto después de aplicar el descuento.</returns>
    public static decimal CalcularImporteLinea(decimal cantidad, decimal precioUnitario, decimal descuentoPct = 0m)
    {
        var bruto = cantidad * precioUnitario;
        return descuentoPct > 0m ? bruto * (1m - descuentoPct / 100m) : bruto;
    }

    /// <summary>
    /// Calcula el monto monetario del descuento de una línea: Bruto × (DescuentoPct / 100).
    /// </summary>
    /// <param name="cantidad">Cantidad de unidades.</param>
    /// <param name="precioUnitario">Precio unitario antes de descuento.</param>
    /// <param name="descuentoPct">Porcentaje de descuento (0–100).</param>
    /// <returns>Monto en moneda del descuento aplicado. 0 si DescuentoPct es 0.</returns>
    public static decimal CalcularDescuentoLinea(decimal cantidad, decimal precioUnitario, decimal descuentoPct)
        => descuentoPct > 0m ? (cantidad * precioUnitario) * (descuentoPct / 100m) : 0m;

    /// <summary>
    /// Calcula los impuestos (IVA) sobre los importes netos de las líneas que aplican.
    /// Fórmula: SUM(ImporteNeto de líneas con IVA) × tasaIva.
    /// </summary>
    /// <param name="lineas">Secuencia de (ImporteNeto, IvaAplicable).</param>
    /// <param name="tasaIva">
    /// Tasa de IVA como decimal (0..1). Obtener desde <c>IConfiguracionFiscalService.ObtenerTasaIvaProductoAsync()</c>
    /// o, como fallback, desde <see cref="FiscalConstants.TasaIvaEstandar"/>.
    /// NUNCA pasar 0.16 directamente — siempre cargar desde configuración.
    /// </param>
    /// <returns>Total de impuestos a aplicar al documento.</returns>
    public static decimal CalcularImpuestos(
        IEnumerable<(decimal ImporteNeto, bool IvaAplicable)> lineas,
        decimal tasaIva)
        => lineas.Where(l => l.IvaAplicable).Sum(l => l.ImporteNeto) * tasaIva;

    /// <summary>
    /// Calcula el total del documento: Subtotal + Impuestos.
    /// </summary>
    public static decimal CalcularTotal(decimal subtotal, decimal impuestos)
        => subtotal + impuestos;
}

namespace Ybridio.Domain.Common;

/// <summary>
/// Constantes fiscales institucionales.
/// Centraliza la tasa estándar de IVA para evitar valores dispersos en el código.
/// Preparada para evolución futura (tasas diferenciales, configuración por empresa).
/// </summary>
/// <remarks>
/// ADR-040 — Operational Commercial Document Standard:
/// Toda lógica de impuestos simple debe referenciar estas constantes,
/// NO hardcodear 0.16 o 16 directamente.
/// </remarks>
public static class FiscalConstants
{
    /// <summary>
    /// Tasa estándar de IVA institucional (16%).
    /// Aplica a productos con <c>IvaAplicable = true</c>.
    /// </summary>
    public const decimal TasaIvaEstandar = 0.16m;
}

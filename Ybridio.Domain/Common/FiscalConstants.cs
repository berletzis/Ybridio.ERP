namespace Ybridio.Domain.Common;

/// <summary>
/// Constantes fiscales de fallback institucional.
///
/// REGLA (Single Source of Truth Fiscal Rule):
/// La fuente de verdad fiscal es <c>catalogos.TipoImpuesto</c> — gestionada vía
/// <c>IConfiguracionFiscalService</c> y <c>ParametroGlobal</c>.
///
/// Esta clase SOLO aplica como:
/// 1. Valor de fallback cuando no hay TipoImpuesto configurado en la empresa.
/// 2. Referencia estática en migraciones o contextos sin sesión activa.
///
/// En código operacional de Application y WinUI SIEMPRE usar:
///   IConfiguracionFiscalService.ObtenerTasaIvaProductoAsync()
///
/// NUNCA hardcodear 0.16 directamente.
/// </summary>
public static class FiscalConstants
{
    /// <summary>
    /// Tasa estándar de IVA de fallback (16%).
    /// SOLO usar cuando no hay TipoImpuesto configurado en IConfiguracionFiscalService.
    /// En operación normal, la tasa debe venir de: TipoImpuesto.Tasa (= Porcentaje / 100).
    /// </summary>
    public const decimal TasaIvaEstandar = 0.16m;
}

namespace Ybridio.Application.Common;

/// <summary>
/// Claves tipadas para ParametroGlobal — análogo a PermisosClave para permisos.
///
/// Regla institucional (Default Tax Configuration Rule):
/// Los parámetros fiscales almacenan el ID del TipoImpuesto (int), NO la tasa directa.
/// Ejemplo: impuesto.default.producto = "3" → TipoImpuestoId = 3 → carga TipoImpuesto → usa Porcentaje.
///
/// SIEMPRE usar estas constantes en el código. NUNCA strings literales.
/// </summary>
public static class ParametrosClave
{
    /// <summary>
    /// Parámetros fiscales — referencian TipoImpuestoId (TipoDato = "int").
    /// Los valores son enteros que apuntan a catalogos.TipoImpuesto.Id.
    /// </summary>
    public static class Fiscal
    {
        /// <summary>
        /// TipoImpuesto default para productos inventariables.
        /// Valor: TipoImpuestoId (int). Fallback si no existe: FiscalConstants.TasaIvaEstandar.
        /// </summary>
        public const string ImpuestoDefaultProducto = "impuesto.default.producto";

        /// <summary>TipoImpuesto default para servicios.</summary>
        public const string ImpuestoDefaultServicio = "impuesto.default.servicio";

        /// <summary>TipoImpuesto default para Otros Cargos documentales (Flete, Maniobras, etc.).</summary>
        public const string ImpuestoDefaultCargo = "impuesto.default.cargo";
    }

    /// <summary>Parámetros de documentos comerciales.</summary>
    public static class Documentos
    {
        /// <summary>Días de vigencia default para cotizaciones (int).</summary>
        public const string VigenciaCotizacionDias = "cotizacion.vigencia.dias";

        /// <summary>Código ISO de moneda base (string). Ej: "MXN".</summary>
        public const string MonedaCodigoDefault = "moneda.codigo.default";

        /// <summary>Símbolo monetario para display (string). Ej: "$".</summary>
        public const string MonedaSimboloDefault = "moneda.simbolo.default";

        /// <summary>Número de decimales monetarios en display (int). Default: 2.</summary>
        public const string MonedaDecimales = "moneda.decimal.digitos";
    }

    /// <summary>Parámetros de series documentales.</summary>
    public static class Series
    {
        /// <summary>Serie default para cotizaciones (string). Ej: "COT".</summary>
        public const string SerieCotizacion = "serie.folio.cotizacion";
    }
}

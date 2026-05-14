using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.DTOs.Catalogos;

namespace Ybridio.Application.Services.Configuracion;

/// <summary>
/// Resuelve la configuración fiscal activa de la empresa en sesión.
///
/// Commercial Tax Pattern — Default Tax Configuration Rule:
/// - ParametroGlobal almacena el TipoImpuestoId de referencia (por contexto: producto, servicio, cargo).
/// - Este servicio carga el TipoImpuesto referenciado y expone la tasa calculada.
/// - CommercialDocumentCalculator recibe la tasa decimal (0..1) de este servicio.
/// - NUNCA hardcodear 0.16 en ViewModels; siempre usar este servicio.
/// - Fallback: si no hay ParametroGlobal configurado, usa FiscalConstants.TasaIvaEstandar.
///
/// Es la capa de integración entre el catálogo fiscal (TipoImpuesto)
/// y la configuración default del sistema (ParametroGlobal).
/// </summary>
public interface IConfiguracionFiscalService
{
    /// <summary>
    /// Obtiene el TipoImpuesto configurado como default para productos inventariables.
    /// Lee <see cref="Common.ParametrosClave.Fiscal.ImpuestoDefaultProducto"/> de ParametroGlobal.
    /// Null si no hay configuración.
    /// </summary>
    Task<TipoImpuestoDto?> ObtenerTipoImpuestoProductoAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene la tasa IVA activa (0..1) para productos.
    /// Fallback: <see cref="Domain.Common.FiscalConstants.TasaIvaEstandar"/> si no hay config.
    /// </summary>
    Task<decimal> ObtenerTasaIvaProductoAsync(CancellationToken ct = default);

    /// <summary>Obtiene el TipoImpuesto default para servicios.</summary>
    Task<TipoImpuestoDto?> ObtenerTipoImpuestoServicioAsync(CancellationToken ct = default);

    /// <summary>Obtiene el TipoImpuesto default para Otros Cargos documentales.</summary>
    Task<TipoImpuestoDto?> ObtenerTipoImpuestoCargoAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene el TipoImpuesto referenciado por cualquier clave de ParametroGlobal de tipo fiscal.
    /// Retorna null si la clave no existe o el TipoImpuesto no se encuentra.
    /// </summary>
    Task<TipoImpuestoDto?> ObtenerTipoImpuestoPorClaveAsync(string claveParametro, CancellationToken ct = default);
}

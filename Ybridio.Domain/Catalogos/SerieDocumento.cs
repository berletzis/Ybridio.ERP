using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

/// <summary>
/// Configura las series documentales del ERP: prefijo, longitud y consecutivo de folios.
/// Cada instancia define la secuencia numérica para un tipo de documento por empresa
/// (y opcionalmente por sucursal para control independiente).
/// </summary>
/// <remarks>
/// Shared Sequence/Folio Pattern:
/// - La generación de folios es ATÓMICA en BD via UPDATE...OUTPUT para garantizar unicidad bajo concurrencia.
/// - NO usar ParametroGlobal para consecutivos runtime — los folios tienen comportamiento transaccional.
/// - Document Identity Rule: la conversión Cotización→Pedido→Venta genera folios nuevos e independientes.
/// - Formato estándar: {Prefijo}-{SiguienteNumero.PadLeft(Longitud, '0')} → "COT-000001"
/// </remarks>
public class SerieDocumento : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }

    /// <summary>
    /// Sucursal específica para esta serie. Null = aplica a todas las sucursales de la empresa.
    /// Permite series independientes por sucursal (preparado para crecimiento futuro).
    /// </summary>
    public int? SucursalId { get; set; }

    /// <summary>Tipo de documento al que aplica esta serie.</summary>
    public TipoDocumentoSerie TipoDocumento { get; set; }

    /// <summary>
    /// Prefijo del folio. Ej: "COT", "PED", "VTA", "ENT", "SAL".
    /// Debe ser único por EmpresaId + TipoDocumento (+ SucursalId si aplica).
    /// </summary>
    public string Prefijo { get; set; } = string.Empty;

    /// <summary>
    /// Número de dígitos del consecutivo (padding de ceros a la izquierda).
    /// Ej: Longitud=6 → "000001", Longitud=8 → "00000001".
    /// </summary>
    public int Longitud { get; set; } = 6;

    /// <summary>
    /// El PRÓXIMO número a asignar. Se incrementa ATÓMICAMENTE en BD al generar cada folio.
    /// Nunca modificar directamente en Application; usar IFolioGeneratorService.
    /// </summary>
    public long SiguienteNumero { get; set; } = 1;

    /// <summary>
    /// Si es true, el consecutivo reinicia a 1 cada año calendario.
    /// SIN implementar en V1 — preparado para crecimiento futuro.
    /// </summary>
    public bool ReinicioAnual { get; set; } = false;

    /// <summary>Año del último reinicio (para ReinicioAnual). Null hasta el primer reinicio.</summary>
    public int? AnioUltimoReinicio { get; set; }

    public bool Activo { get; set; } = true;

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public Sucursal? Sucursal { get; set; }
}

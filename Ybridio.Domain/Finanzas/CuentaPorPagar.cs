using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Finanzas;

/// <summary>
/// Cuenta por pagar simple para control operativo de obligaciones con proveedores o acreedores.
/// No es una póliza contable; es un registro operativo de lo que debemos.
/// <para>
/// SaldoPendiente = MontoOriginal - MontoPagado (calculado en Application layer, no almacenado).
/// EsVencida = FechaVencimiento &lt; DateTime.Today AND MontoPagado &lt; MontoOriginal.
/// </para>
/// </summary>
public class CuentaPorPagar : AuditableEntity
{
    public long   Id               { get; set; }
    public int    EmpresaId        { get; set; }
    public int?   SucursalId       { get; set; }

    /// <summary>Nombre del proveedor o acreedor — almacenado aunque no exista FK a Proveedor.</summary>
    public string NombreAcreedor   { get; set; } = string.Empty;

    /// <summary>Descripción de la obligación (p.ej. "Renta local julio", "Préstamo bancario cuota 3").</summary>
    public string  Concepto         { get; set; } = string.Empty;
    public decimal MontoOriginal    { get; set; }
    public decimal MontoPagado      { get; set; }
    public DateTime FechaEmision    { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public string?  Observaciones   { get; set; }

    // Navegación
    public Empresa   Empresa   { get; set; } = null!;
    public Sucursal? Sucursal  { get; set; }
}

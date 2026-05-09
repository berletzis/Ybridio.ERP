using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Finanzas;

/// <summary>
/// Cuenta por cobrar simple para control operativo de saldos pendientes de clientes o deudores.
/// No es una factura formal; es un registro operativo de lo que nos deben.
/// <para>
/// SaldoPendiente = MontoOriginal - MontoPagado (calculado en Application layer, no almacenado).
/// EsVencida = FechaVencimiento &lt; DateTime.Today AND MontoPagado &lt; MontoOriginal.
/// </para>
/// </summary>
public class CuentaPorCobrar : AuditableEntity
{
    public long   Id               { get; set; }
    public int    EmpresaId        { get; set; }
    public int?   SucursalId       { get; set; }

    /// <summary>Nombre del cliente o deudor — almacenado aunque no exista FK a Cliente.</summary>
    public string NombreDeudor     { get; set; } = string.Empty;

    /// <summary>Descripción del adeudo (p.ej. "Factura 0045", "Préstamo personal", "Venta a crédito").</summary>
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

using Ybridio.Domain.Common;

namespace Ybridio.Domain.Finanzas;

public class MovimientoCaja : CreationAuditEntity
{
    public long Id { get; set; }
    public int CajaId { get; set; }
    public string? Tipo { get; set; }
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; }
    public string? Referencia { get; set; }
    public int? TipoMovimientoId { get; set; }

    // Navegación
    public Caja Caja { get; set; } = null!;
    public TipoMovimientoCaja? TipoMovimiento { get; set; }
}

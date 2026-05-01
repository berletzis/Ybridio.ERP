using Ybridio.Domain.Common;
using Ybridio.Domain.Core;
using Ybridio.Domain.Finanzas;

namespace Ybridio.Domain.Ventas;

public class Venta : AuditableEntity
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public int TiendaId { get; set; }
    public DateTime Fecha { get; set; }
    public decimal? Total { get; set; }
    public int? CajaId { get; set; }
    public int? AperturaCajaId { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public Tienda Tienda { get; set; } = null!;
    public Caja? Caja { get; set; }
    public AperturaCaja? AperturaCaja { get; set; }
    public ICollection<VentaDetalle> Detalles { get; set; } = [];
    public ICollection<Factura> Facturas { get; set; } = [];
}

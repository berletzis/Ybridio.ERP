using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Ventas;

public class Factura : CreationAuditEntity
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public int ClienteId { get; set; }
    public long? VentaId { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public string? UUID { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public Cliente Cliente { get; set; } = null!;
    public Venta? Venta { get; set; }
}

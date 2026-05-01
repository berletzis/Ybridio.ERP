using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Compras;

public class RecepcionCompra : CreationAuditEntity
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public long OrdenCompraId { get; set; }
    public DateTime Fecha { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public OrdenCompra OrdenCompra { get; set; } = null!;
    public ICollection<RecepcionCompraDetalle> Detalles { get; set; } = [];
}

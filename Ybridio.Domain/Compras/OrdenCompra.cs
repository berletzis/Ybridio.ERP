using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;
using Ybridio.Domain.Core;
using Ybridio.Domain.Enums;

namespace Ybridio.Domain.Compras;

public class OrdenCompra : AuditableEntity
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public int ProveedorId { get; set; }
    /// <summary>Folio documental (ej: "OC-000001"). Null en registros anteriores a SerieDocumento.</summary>
    public string? Folio { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public EstatusOrdenCompra Estatus { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public Proveedor Proveedor { get; set; } = null!;
    public ICollection<OrdenCompraDetalle> Detalles { get; set; } = [];
    public ICollection<RecepcionCompra> Recepciones { get; set; } = [];
}

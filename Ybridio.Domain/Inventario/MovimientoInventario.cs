using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Inventario;

public class MovimientoInventario : AuditableEntity
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public int AlmacenId { get; set; }
    public int ProductoId { get; set; }
    public int TipoMovimientoId { get; set; }
    public decimal Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal Total { get; set; }
    public string? Referencia { get; set; }
    public long? ReferenciaId { get; set; }
    public DateTime Fecha { get; set; }
    public int? SucursalId { get; set; }
    public decimal SaldoAcumulado { get; set; }
    public string? Folio { get; set; }
    public string? Observaciones { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public Almacen Almacen { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
    public Sucursal? Sucursal { get; set; }
    public TipoMovimientoInventario TipoMovimiento { get; set; } = null!;
}

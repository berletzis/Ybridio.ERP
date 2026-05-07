using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;

namespace Ybridio.Domain.Inventario;

/// <summary>Línea de detalle de un ajuste de inventario con diferencia calculada.</summary>
public class AjusteInventarioDetalle : CreationAuditEntity
{
    public long Id { get; set; }
    public long AjusteInventarioId { get; set; }
    public int ProductoId { get; set; }
    public decimal CantidadSistema { get; set; }
    public decimal CantidadFisica { get; set; }
    /// <summary>Columna calculada persistida en BD: CantidadFisica - CantidadSistema.</summary>
    public decimal? Diferencia { get; set; }
    public decimal CostoUnitario { get; set; }
    public string? Observaciones { get; set; }

    // Navegación
    public AjusteInventario AjusteInventario { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}

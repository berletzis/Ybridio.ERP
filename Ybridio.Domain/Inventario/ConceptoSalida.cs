using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Inventario;

/// <summary>Catálogo de motivos de salida: venta, ajuste, merma, traspaso, devolución a proveedor, etc.</summary>
public class ConceptoSalida : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool AfectaExistencia { get; set; } = true;
    public bool EsTraspaso { get; set; }
    public bool EsVenta { get; set; }
    public bool Activo { get; set; } = true;

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public ICollection<Salida> Salidas { get; set; } = [];
}

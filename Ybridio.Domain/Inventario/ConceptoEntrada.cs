using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Inventario;

/// <summary>Catálogo de motivos de entrada: compra, ajuste, traspaso, devolución de cliente, etc.</summary>
public class ConceptoEntrada : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool AfectaExistencia { get; set; } = true;
    public bool RequiereOrdenCompra { get; set; }
    public bool EsTraspaso { get; set; }
    public bool Activo { get; set; } = true;

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public ICollection<Entrada> Entradas { get; set; } = [];
}

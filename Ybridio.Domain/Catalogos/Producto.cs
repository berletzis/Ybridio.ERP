using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

public class Producto : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public bool Activo { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
}

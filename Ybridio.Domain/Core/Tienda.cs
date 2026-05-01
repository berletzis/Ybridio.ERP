using Ybridio.Domain.Common;

namespace Ybridio.Domain.Core;

public class Tienda : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;

    // Navegación
    public Empresa Empresa { get; set; } = null!;
}

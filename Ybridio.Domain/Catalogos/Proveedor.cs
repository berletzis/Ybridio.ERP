using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

public class Proveedor : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? RFC { get; set; }
    public string? Email { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
}

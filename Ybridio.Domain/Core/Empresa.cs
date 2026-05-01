using Ybridio.Domain.Common;

namespace Ybridio.Domain.Core;

public class Empresa : AuditableEntity
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? RFC { get; set; }

    // Navegación
    public ICollection<Tienda> Tiendas { get; set; } = [];
}

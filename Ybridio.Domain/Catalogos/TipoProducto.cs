// ── Ybridio.Domain/Catalogos/TipoProducto.cs ─────────────────────────────────
using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

public class TipoProducto : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;

    // Navegación
    public Empresa Empresa { get; set; } = null!;
}
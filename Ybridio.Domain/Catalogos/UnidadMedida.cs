// ── Ybridio.Domain/Catalogos/UnidadMedida.cs ─────────────────────────────────
using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

public class UnidadMedida : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Abreviatura { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    // Navegación
    public Empresa Empresa { get; set; } = null!;
}
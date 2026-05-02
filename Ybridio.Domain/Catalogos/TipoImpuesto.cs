
// ── Ybridio.Domain/Catalogos/TipoImpuesto.cs ─────────────────────────────────
using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

public class TipoImpuesto : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;   // "IVA 16%", "Exento"
    public decimal Porcentaje { get; set; }               // 16.00, 8.00, 0.00
    public bool Activo { get; set; } = true;

    // Navegación
    public Empresa Empresa { get; set; } = null!;
}

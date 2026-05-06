using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Finanzas;

public class Caja : CreationAuditEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int SucursalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Saldo { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<AperturaCaja> Aperturas { get; set; } = [];
    public ICollection<MovimientoCaja> Movimientos { get; set; } = [];
}

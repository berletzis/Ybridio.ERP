using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Inventario;

/// <summary>Encabezado de un ajuste de inventario (positivo o negativo).</summary>
public class AjusteInventario : AuditableEntity
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public int SucursalId { get; set; }
    public int AlmacenId { get; set; }
    public string? Folio { get; set; }
    public DateTime Fecha { get; set; }
    /// <summary>1 = positivo (suma), -1 = negativo (resta).</summary>
    public short TipoAjuste { get; set; } = 1;
    public string Motivo { get; set; } = string.Empty;
    public Guid? UsuarioAutorizacionId { get; set; }
    public bool Aplicado { get; set; }
    public DateTime? FechaAplicacion { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
    public Almacen Almacen { get; set; } = null!;
    public ICollection<AjusteInventarioDetalle> Detalles { get; set; } = [];
}

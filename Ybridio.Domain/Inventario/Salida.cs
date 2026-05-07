using Ybridio.Domain.Common;
using Ybridio.Domain.Core;
using Ybridio.Domain.Ventas;

namespace Ybridio.Domain.Inventario;

/// <summary>Encabezado de una salida de almacén (venta, ajuste, merma, traspaso).</summary>
public class Salida : AuditableEntity
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public int SucursalId { get; set; }
    public int AlmacenId { get; set; }
    public int ConceptoSalidaId { get; set; }
    public int EstatusSalidaId { get; set; }
    public string? Folio { get; set; }
    public DateTime Fecha { get; set; }
    public long? VentaId { get; set; }
    public int? AlmacenDestinoId { get; set; }
    public Guid? UsuarioAutorizacionId { get; set; }
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }
    public bool Aplicada { get; set; }
    public DateTime? FechaAplicacion { get; set; }
    public Guid? UsuarioAplicacionId { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
    public Almacen Almacen { get; set; } = null!;
    public Almacen? AlmacenDestino { get; set; }
    public ConceptoSalida ConceptoSalida { get; set; } = null!;
    public EstatusSalida EstatusSalida { get; set; } = null!;
    public Venta? Venta { get; set; }
    public ICollection<SalidaDetalle> Detalles { get; set; } = [];
}

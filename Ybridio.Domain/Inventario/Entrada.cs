using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;
using Ybridio.Domain.Compras;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Inventario;

/// <summary>Encabezado de una entrada de almacén (compra, ajuste, traspaso, devolución).</summary>
public class Entrada : AuditableEntity
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public int SucursalId { get; set; }
    public int AlmacenId { get; set; }
    public int ConceptoEntradaId { get; set; }
    public int EstatusEntradaId { get; set; }
    public string? Folio { get; set; }
    public DateTime Fecha { get; set; }
    public DateTime? FechaRecepcion { get; set; }
    public string? ReferenciaExterna { get; set; }
    public string? NumeroFactura { get; set; }
    public int? ProveedorId { get; set; }
    public long? OrdenCompraId { get; set; }
    public int? AlmacenOrigenId { get; set; }
    public long? SalidaOrigenId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TotalImpuestos { get; set; }
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }
    public bool Aplicada { get; set; }
    public DateTime? FechaAplicacion { get; set; }
    public Guid? UsuarioAplicacionId { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
    public Almacen Almacen { get; set; } = null!;
    public Almacen? AlmacenOrigen { get; set; }
    public ConceptoEntrada ConceptoEntrada { get; set; } = null!;
    public EstatusEntrada EstatusEntrada { get; set; } = null!;
    public Proveedor? Proveedor { get; set; }
    public OrdenCompra? OrdenCompra { get; set; }
    public Salida? SalidaOrigen { get; set; }
    public ICollection<EntradaDetalle> Detalles { get; set; } = [];
}

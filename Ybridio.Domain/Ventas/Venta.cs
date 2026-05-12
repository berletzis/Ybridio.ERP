using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;
using Ybridio.Domain.Core;
using Ybridio.Domain.Finanzas;

namespace Ybridio.Domain.Ventas;

public class Venta : AuditableEntity
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public int SucursalId { get; set; }
    public DateTime Fecha { get; set; }
    public decimal? Total { get; set; }
    public int? CajaId { get; set; }
    public int? AperturaCajaId { get; set; }

    // ── Campos del flujo documental (nullable para compatibilidad con registros POS existentes) ──
    /// <summary>FK al cliente del catálogo. Null para ventas POS sin registro de cliente.</summary>
    public int? RelacionComercialId { get; set; }
    /// <summary>Nombre congelado del cliente al momento de crear la venta.</summary>
    public string? NombreCliente { get; set; }
    /// <summary>Referencia al pedido origen. Null si la venta se crea directamente.</summary>
    public long? PedidoId { get; set; }
    /// <summary>Estatus del documento de venta. POS legacy queda en Confirmada(1).</summary>
    public EstatusVenta Estatus { get; set; } = EstatusVenta.Borrador;
    /// <summary>Tipo de pago: Contado(0) o Crédito(1).</summary>
    public TipoPago TipoPago { get; set; } = TipoPago.Contado;
    /// <summary>
    /// Subtotal = SUM(detalles.Importe). V1 = Total.
    /// Persistido para acceso rápido en listados.
    /// </summary>
    public decimal? Subtotal { get; set; }
    /// <summary>
    /// Total pagado acumulado. Se incrementa con cada PagoVenta registrado.
    /// SaldoPendiente = Total - TotalPagado (calculado en runtime, no almacenado).
    /// </summary>
    public decimal TotalPagado { get; set; }
    public string? Observaciones { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
    public Caja? Caja { get; set; }
    public AperturaCaja? AperturaCaja { get; set; }
    public RelacionComercial? RelacionComercial { get; set; }
    public ICollection<VentaDetalle> Detalles { get; set; } = [];
    public ICollection<Factura> Facturas { get; set; } = [];
    public ICollection<PagoVenta> Pagos { get; set; } = [];
}

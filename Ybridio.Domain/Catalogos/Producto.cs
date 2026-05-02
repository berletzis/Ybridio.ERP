// ── Ybridio.Domain/Catalogos/Producto.cs — REEMPLAZAR COMPLETO ───────────────
using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

public class Producto : AuditableEntity
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }

    // ── Identificación ────────────────────────────────────────────────────────
    public string Codigo { get; set; } = string.Empty;
    public string? CodigoBarras { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    // ── Precios y costos ──────────────────────────────────────────────────────
    public decimal Precio { get; set; }          // Precio de venta
    public decimal? PrecioMinimo { get; set; }   // Precio mínimo permitido
    public decimal? Costo { get; set; }          // Precio de compra / costo

    // ── Impuesto ──────────────────────────────────────────────────────────────
    public bool IvaAplicable { get; set; } = true;
    public int? TipoImpuestoId { get; set; }

    // ── Clasificación ─────────────────────────────────────────────────────────
    public int? CategoriaId { get; set; }
    public int? TipoProductoId { get; set; }
    public int? UnidadMedidaId { get; set; }

    // ── Inventario ────────────────────────────────────────────────────────────
    public decimal? StockMinimo { get; set; }
    public decimal? StockMaximo { get; set; }

    // ── Proveedor predeterminado ──────────────────────────────────────────────
    public int? ProveedorId { get; set; }

    // ── Estado ────────────────────────────────────────────────────────────────
    public bool Activo { get; set; } = true;

    // ── Navegación ────────────────────────────────────────────────────────────
    public Empresa Empresa { get; set; } = null!;
    public TipoImpuesto? TipoImpuesto { get; set; }
    public CategoriaProducto? Categoria { get; set; }
    public TipoProducto? TipoProducto { get; set; }
    public UnidadMedida? UnidadMedida { get; set; }
    public Proveedor? Proveedor { get; set; }
}
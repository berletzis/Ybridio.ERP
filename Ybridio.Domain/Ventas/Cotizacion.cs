using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Ventas;

/// <summary>
/// Propuesta comercial emitida al cliente antes de convertirse en pedido o venta.
/// Una cotización puede incluir productos del catálogo y servicios ad-hoc.
/// <para>
/// Flujo: Borrador → Enviada → Aprobada → (origina Pedido o Venta directa) | Cancelada.
/// </para>
/// </summary>
/// <remarks>
/// NombreCliente se almacena denormalizado para conservar el nombre exacto al momento
/// de cotizar, incluso si el registro de cliente cambia posteriormente.
/// ClienteId es nullable para soportar clientes de mostrador sin registro.
/// </remarks>
public class Cotizacion : AuditableEntity
{
    public long  Id          { get; set; }
    public int   EmpresaId   { get; set; }
    public int?  SucursalId  { get; set; }

    public int?   ClienteId     { get; set; }
    /// <summary>Nombre del cliente al momento de cotizar (denormalizado).</summary>
    public string NombreCliente { get; set; } = string.Empty;

    public EstatusCotizacion Estatus     { get; set; } = EstatusCotizacion.Borrador;
    public DateTime          Fecha       { get; set; }
    public DateTime?         FechaVigencia { get; set; }

    /// <summary>ID del usuario vendedor que emite la cotización.</summary>
    public Guid?  VendedorId    { get; set; }

    /// <summary>
    /// Subtotal antes de impuestos.
    /// Fórmula: SUM(detalle.Importe)
    /// Calculado y almacenado al guardar para evitar recalcular en cada lectura.
    /// </summary>
    public decimal Subtotal     { get; set; }

    /// <summary>
    /// Total final de la cotización.
    /// Fórmula: Subtotal + impuestos (en V1 = Subtotal, sin cálculo de IVA independiente).
    /// Se persiste en BD para facilitar reportes sin recalcular detalles.
    /// </summary>
    public decimal Total        { get; set; }

    public string? Observaciones { get; set; }

    // Navegación
    public Empresa   Empresa   { get; set; } = null!;
    public Sucursal? Sucursal  { get; set; }
    public Cliente?  Cliente   { get; set; }
    public ICollection<CotizacionDetalle> Detalles { get; set; } = [];
}

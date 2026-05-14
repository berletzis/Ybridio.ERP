using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Ventas;

/// <summary>
/// Orden de trabajo operativa ligera para talleres, servicios técnicos, reparaciones e instalaciones.
/// Diseñada para PYME: simple, operacional, sin manufactura avanzada ni MRP.
/// <para>
/// Flujo de estados: Nueva → EnProceso → [EsperandoMaterial →] Terminada → Entregada | Cancelada.
/// </para>
/// </summary>
/// <remarks>
/// La OrdenTrabajo NO es manufactura industrial. Es el registro operativo de:
/// - Qué se va a hacer / se hizo (Descripcion)
/// - Quién lo hace (ResponsableId)
/// - Qué materiales se usaron (OrdenTrabajoMateriales)
/// - Cuándo se comprometió la entrega (FechaCompromiso)
///
/// El Total se calcula como SUM(materiales.Importe). En V1 no incluye mano de obra como ítem
/// separado (se incluye como material ad-hoc si el taller lo necesita).
/// </remarks>
public class OrdenTrabajo : AuditableEntity
{
    public long  Id          { get; set; }
    public int   EmpresaId   { get; set; }
    public int?  SucursalId  { get; set; }

    public int?   RelacionComercialId     { get; set; }
    /// <summary>Nombre del cliente (denormalizado para mostrar sin join).</summary>
    public string NombreCliente { get; set; } = string.Empty;

    /// <summary>Pedido de origen si la OT fue generada desde un pedido.</summary>
    public long? PedidoId     { get; set; }

    /// <summary>Folio documental (ej: "OT-000001"). Null en registros anteriores a SerieDocumento.</summary>
    public string? Folio { get; set; }

    public EstatusOrdenTrabajo Estatus        { get; set; } = EstatusOrdenTrabajo.Nueva;
    public DateTime            Fecha          { get; set; }
    public DateTime?           FechaCompromiso { get; set; }

    /// <summary>Descripción del trabajo a realizar o realizado.</summary>
    public string  Descripcion   { get; set; } = string.Empty;
    public string? Observaciones  { get; set; }

    /// <summary>Usuario responsable de ejecutar la orden.</summary>
    public Guid?  ResponsableId  { get; set; }

    /// <summary>
    /// Total de la orden de trabajo.
    /// Fórmula: SUM(materiales.Importe). Calculado y persistido al guardar materiales.
    /// En V1 = suma de materiales; mano de obra se agrega como material ad-hoc si aplica.
    /// </summary>
    public decimal Total          { get; set; }

    // Navegación
    public Empresa   Empresa   { get; set; } = null!;
    public Sucursal? Sucursal  { get; set; }
    public RelacionComercial?  RelacionComercial   { get; set; }
    public Pedido?   Pedido    { get; set; }
    public ICollection<OrdenTrabajoMaterial> Materiales { get; set; } = [];
}

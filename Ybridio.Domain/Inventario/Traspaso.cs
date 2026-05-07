using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Inventario;

/// <summary>
/// Vincula una Salida (almacén origen) con una Entrada (almacén destino)
/// en un traspaso entre almacenes. Trackea el estado global del movimiento.
/// </summary>
public class Traspaso : AuditableEntity
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public string? Folio { get; set; }
    public DateTime Fecha { get; set; }
    public int AlmacenOrigenId { get; set; }
    public int AlmacenDestinoId { get; set; }
    public long? SalidaId { get; set; }
    public long? EntradaId { get; set; }
    /// <summary>1=Pendiente, 2=En Tránsito, 3=Recibido, 4=Cancelado.</summary>
    public int Estatus { get; set; } = 1;
    public string? Observaciones { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public Almacen AlmacenOrigen { get; set; } = null!;
    public Almacen AlmacenDestino { get; set; } = null!;
    public Salida? Salida { get; set; }
    public Entrada? Entrada { get; set; }
}

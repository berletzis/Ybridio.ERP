namespace Ybridio.Domain.Catalogos;

/// <summary>
/// Tipos de documento comercial que pueden tener una SerieDocumento asignada.
/// Cada tipo mantiene su propio consecutivo de folios independiente.
/// </summary>
/// <remarks>
/// Document Identity Rule: cada conversión documental genera un folio nuevo e independiente.
/// COT-000001 → PED-000001 → VTA-000001 (identidades distintas, trazabilidad por referencia cruzada).
/// </remarks>
public enum TipoDocumentoSerie
{
    Cotizacion       = 1,
    Pedido           = 2,
    Venta            = 3,
    OrdenTrabajo     = 4,
    EntradaAlmacen   = 5,
    SalidaAlmacen    = 6,
    OrdenCompra      = 7,
    ConteoInventario = 8,
    Traspaso         = 9,
    AjusteInventario = 10,
}

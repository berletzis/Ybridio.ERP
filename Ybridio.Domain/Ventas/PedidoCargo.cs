namespace Ybridio.Domain.Ventas;

/// <summary>
/// Cargo accesorio de un pedido (Flete, Maniobras, Seguro, etc.).
/// Commercial Charges Pattern (ADR-054) aplicado al módulo Pedidos.
/// <para>
/// Los cargos son conceptos que impactan el Total del pedido pero NO son productos
/// del catálogo. Se gestionan en sección separada del grid de detalles.
/// </para>
/// </summary>
public class PedidoCargo
{
    public long    Id          { get; set; }
    public long    PedidoId    { get; set; }

    /// <summary>Descripción del cargo (ej: "Flete", "Maniobras", "Seguro").</summary>
    public string  Descripcion { get; set; } = string.Empty;

    /// <summary>Monto del cargo. Sin IVA.</summary>
    public decimal Importe     { get; set; }

    /// <summary>Indica si este cargo aplica IVA para el cálculo de impuestos del documento.</summary>
    public bool    AplicaIva   { get; set; }

    /// <summary>Orden de presentación en la sección de cargos.</summary>
    public int     Orden       { get; set; }

    // Navegación
    public Pedido Pedido { get; set; } = null!;
}

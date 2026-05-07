namespace Ybridio.Domain.Inventario;

public class TipoMovimientoInventario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool AfectaExistencia { get; set; }
    /// <summary>1 = entrada (suma stock), -1 = salida (resta stock).</summary>
    public short Signo { get; set; } = 1;
    public string? Descripcion { get; set; }

    // Navegación
    public ICollection<MovimientoInventario> Movimientos { get; set; } = [];
}

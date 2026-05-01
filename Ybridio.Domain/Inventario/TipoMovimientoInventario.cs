namespace Ybridio.Domain.Inventario;

public class TipoMovimientoInventario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool AfectaExistencia { get; set; }

    // Navegación
    public ICollection<MovimientoInventario> Movimientos { get; set; } = [];
}

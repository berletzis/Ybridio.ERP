namespace Ybridio.Domain.Finanzas;

public class TipoMovimientoCaja
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    // Navegación
    public ICollection<MovimientoCaja> Movimientos { get; set; } = [];
}

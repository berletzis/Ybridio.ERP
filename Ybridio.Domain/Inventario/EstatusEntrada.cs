namespace Ybridio.Domain.Inventario;

/// <summary>Catálogo global de estatus de entrada (Pendiente, Recibida, Recibida Parcial, Cancelada).</summary>
public class EstatusEntrada
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    // Navegación
    public ICollection<Entrada> Entradas { get; set; } = [];
}

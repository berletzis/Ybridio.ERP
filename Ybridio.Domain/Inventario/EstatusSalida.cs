namespace Ybridio.Domain.Inventario;

/// <summary>Catálogo global de estatus de salida (Pendiente, Aplicada, Cancelada).</summary>
public class EstatusSalida
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    // Navegación
    public ICollection<Salida> Salidas { get; set; } = [];
}

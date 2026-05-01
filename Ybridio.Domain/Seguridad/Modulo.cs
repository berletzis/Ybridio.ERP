namespace Ybridio.Domain.Seguridad;

public class Modulo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
    public int Orden { get; set; }
    public DateTime FechaCreacion { get; set; }
    public bool Borrado { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navegación
    public ICollection<Permiso> Permisos { get; set; } = [];
}

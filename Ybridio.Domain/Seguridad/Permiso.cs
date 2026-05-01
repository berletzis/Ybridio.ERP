namespace Ybridio.Domain.Seguridad;

public class Permiso
{
    public int Id { get; set; }
    public int ModuloId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public bool Borrado { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navegación
    public Modulo Modulo { get; set; } = null!;
    public ICollection<RolPermiso> RolPermisos { get; set; } = [];
    public ICollection<UsuarioPermiso> UsuarioPermisos { get; set; } = [];
}

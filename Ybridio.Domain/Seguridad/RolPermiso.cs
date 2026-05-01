namespace Ybridio.Domain.Seguridad;

public class RolPermiso
{
    public int Id { get; set; }
    public Guid RolId { get; set; }
    public int PermisoId { get; set; }
    public bool Permitido { get; set; }

    // Navegación — ApplicationRole vive en Infrastructure; la relación se configura vía Fluent API
    public Permiso Permiso { get; set; } = null!;
}

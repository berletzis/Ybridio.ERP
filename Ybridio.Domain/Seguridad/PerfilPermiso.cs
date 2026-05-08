namespace Ybridio.Domain.Seguridad;

/// <summary>
/// Tabla de unión N:N entre <see cref="Perfil"/> y <see cref="Permiso"/>.
/// No hereda de entidad de auditoría — sin soft-delete (eliminación directa).
/// </summary>
public class PerfilPermiso
{
    public int Id       { get; set; }
    public int PerfilId  { get; set; }
    public int PermisoId { get; set; }

    // Navegación
    public Perfil  Perfil  { get; set; } = null!;
    public Permiso Permiso { get; set; } = null!;
}

namespace Ybridio.Domain.Seguridad;

public class UsuarioPermiso
{
    public int Id { get; set; }
    public Guid UsuarioId { get; set; }
    public int PermisoId { get; set; }

    /// <summary>
    /// Null = hereda del rol; true = sobrescribe a permitido; false = sobrescribe a denegado.
    /// </summary>
    public bool? Permitido { get; set; }

    // Navegación — ApplicationUser vive en Infrastructure; la relación se configura vía Fluent API
    public Permiso Permiso { get; set; } = null!;
}

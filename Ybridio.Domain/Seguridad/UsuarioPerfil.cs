namespace Ybridio.Domain.Seguridad;

/// <summary>
/// Asignación directa de un <see cref="Perfil"/> a un usuario.
/// Permite agrupar permisos reutilizables sin modificar los roles del usuario.
/// <c>UsuarioId</c> referencia a <c>ApplicationUser</c> (en Infrastructure);
/// la relación se configura vía Fluent API sin navegación directa al usuario.
/// </summary>
public class UsuarioPerfil
{
    public int  Id        { get; set; }
    public Guid UsuarioId { get; set; }
    public int  PerfilId  { get; set; }

    // Navegación
    public Perfil Perfil { get; set; } = null!;
}

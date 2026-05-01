using Ybridio.Domain.Core;

namespace Ybridio.Domain.Seguridad;

public class UsuarioTienda
{
    public int Id { get; set; }
    public Guid UsuarioId { get; set; }
    public int? TiendaId { get; set; }

    // Navegación — ApplicationUser vive en Infrastructure; la relación se configura vía Fluent API
    public Tienda? Tienda { get; set; }
}

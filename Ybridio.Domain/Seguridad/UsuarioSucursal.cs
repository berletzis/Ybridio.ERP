using Ybridio.Domain.Core;

namespace Ybridio.Domain.Seguridad;

public class UsuarioSucursal
{
    public int Id { get; set; }
    public Guid UsuarioId { get; set; }
    public int? SucursalId { get; set; }

    // Navegación — ApplicationUser vive en Infrastructure; la relación se configura vía Fluent API
    public Sucursal? Sucursal { get; set; }
}

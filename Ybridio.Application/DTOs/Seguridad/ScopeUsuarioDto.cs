namespace Ybridio.Application.DTOs.Seguridad;

/// <summary>
/// DTO de scopes de acceso de un usuario. Muestra a qué sucursales y almacenes tiene acceso.
/// EsSuperAdmin = true indica acceso irrestricto a todos los scopes.
/// </summary>
public sealed record ScopeUsuarioDto(
    Guid   UsuarioId,
    string Nombre,
    bool   EsSuperAdmin,
    int    CantSucursales,
    int    CantAlmacenes,
    string SucursalesTexto,
    string AlmacenesTexto);

/// <summary>DTO simple para sucursal en contexto de administración de scopes.</summary>
public sealed record SucursalScopeItem(int Id, string Nombre);

/// <summary>DTO simple para almacén en contexto de administración de scopes.</summary>
public sealed record AlmacenScopeItem(int Id, string Nombre);

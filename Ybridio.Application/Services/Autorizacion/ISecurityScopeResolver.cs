namespace Ybridio.Application.Services.Autorizacion;

/// <summary>
/// Resuelve los scopes de seguridad de un usuario:
/// a qué sucursales y almacenes tiene acceso, y si es SuperAdmin.
/// Los scopes controlan el AISLAMIENTO de datos, no los permisos de acción.
/// </summary>
/// <remarks>
/// Jerarquía de scopes: Empresa → Sucursal → Almacén.
/// Un SuperAdmin ve todo dentro de su empresa.
/// Un usuario normal solo accede a sus sucursales asignadas (UsuarioSucursal)
/// y dentro de éstas, solo a sus almacenes asignados (UsuarioAlmacen), si tiene alguno.
/// Si no tiene almacenes asignados pero sí sucursales → accede a todos los almacenes de su sucursal.
/// </remarks>
public interface ISecurityScopeResolver
{
    /// <summary>
    /// Retorna true si el usuario puede acceder a la sucursal indicada.
    /// Los SuperAdmins siempre retornan true para cualquier sucursal de su empresa.
    /// </summary>
    Task<bool> TieneAccesoSucursalAsync(Guid usuarioId, int sucursalId, CancellationToken ct = default);

    /// <summary>
    /// Retorna true si el usuario puede acceder al almacén indicado.
    /// </summary>
    Task<bool> TieneAccesoAlmacenAsync(Guid usuarioId, int almacenId, CancellationToken ct = default);

    /// <summary>
    /// Retorna los IDs de sucursales a las que el usuario tiene acceso.
    /// Lista vacía = SuperAdmin (acceso a todas, sin restricción de lista).
    /// </summary>
    Task<IReadOnlyList<int>> ObtenerSucursalesPermitidasAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Retorna los IDs de almacenes a los que el usuario tiene acceso explícito.
    /// Lista vacía = sin restricción de almacén (accede a todos los de sus sucursales).
    /// </summary>
    Task<IReadOnlyList<int>> ObtenerAlmacentesPermitidosAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Retorna true si el usuario tiene el rol SuperAdmin.
    /// Los SuperAdmins omiten los filtros de sucursal/almacén.
    /// </summary>
    Task<bool> EsSuperAdminAsync(Guid usuarioId, CancellationToken ct = default);
}

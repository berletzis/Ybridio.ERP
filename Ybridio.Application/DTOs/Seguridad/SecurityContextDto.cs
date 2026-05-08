namespace Ybridio.Application.DTOs.Seguridad;

/// <summary>
/// Snapshot del contexto de seguridad efectivo de un usuario en la sesión activa.
/// Incluye permisos, roles, scopes empresa/sucursal/almacén y perfiles asignados.
/// </summary>
public sealed record SecurityContextDto(
    Guid                  UsuarioId,
    string                UsuarioNombre,
    int                   EmpresaId,
    int                   SucursalId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Perfiles,
    IReadOnlySet<string>  PermisosEfectivos,
    IReadOnlyList<int>    SucursalesPermitidas,
    IReadOnlyList<int>    AlmacentesPermitidos,
    bool                  EsSuperAdmin,
    DateTime              GeneradoEn);

namespace Ybridio.Infrastructure.Persistence;

/// <summary>
/// Abstracción del contexto activo de sesión accesible desde Infrastructure y Application.
/// Implementada por SessionService (WinUI) — inyectada como Singleton en DbContext y servicios.
/// </summary>
public interface ISessionContext
{
    /// <summary>Empresa activa. 0 = sin sesión / design-time (filtros globales desactivados).</summary>
    int EmpresaId { get; }

    /// <summary>Sucursal activa. 0 = sin selección.</summary>
    int SucursalId { get; }

    /// <summary>ID del usuario autenticado. Null si no hay sesión activa.</summary>
    Guid? UsuarioId { get; }
}

/// <summary>
/// Contexto nulo para design-time (dotnet ef migrations) y tests.
/// EmpresaId == 0 → los filtros globales de empresa se desactivan.
/// </summary>
public sealed class NullSessionContext : ISessionContext
{
    public static readonly ISessionContext Instance = new NullSessionContext();
    public int   EmpresaId  => 0;
    public int   SucursalId => 0;
    public Guid? UsuarioId  => null;
}

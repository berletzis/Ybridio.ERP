namespace Ybridio.Infrastructure.Persistence;

/// <summary>
/// Abstracción del contexto activo de sesión accesible desde Infrastructure.
/// Implementada por SessionService (WinUI) — inyectada como Singleton en DbContext.
/// </summary>
public interface ISessionContext
{
    int EmpresaId { get; }
    int SucursalId { get; }
}

/// <summary>
/// Contexto nulo para design-time (dotnet ef migrations) y tests.
/// EmpresaId == 0 → los filtros globales de empresa se desactivan.
/// </summary>
public sealed class NullSessionContext : ISessionContext
{
    public static readonly ISessionContext Instance = new NullSessionContext();
    public int EmpresaId => 0;
    public int SucursalId  => 0;
}

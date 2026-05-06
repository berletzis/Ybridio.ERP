using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ybridio.Infrastructure.Persistence;

/// <summary>
/// Factory para herramientas de diseño de EF Core (dotnet ef migrations).
/// Solo se usa en tiempo de diseño — nunca en producción.
/// </summary>
public sealed class ErpDbContextFactory : IDesignTimeDbContextFactory<ErpDbContext>
{
    public ErpDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseSqlServer(
                "Server=132.148.74.136\\ybridio;Database=YBRIDIO-26;user id=sa;password=U3xc3pt!0n!22;TrustServerCertificate=True;MultipleActiveResultSets=true",
                sql => sql.MigrationsAssembly("Ybridio.Infrastructure"))
            .Options;

        return new ErpDbContext(options, NullSessionContext.Instance);
    }
}

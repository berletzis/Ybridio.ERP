using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ybridio.Infrastructure.Persistence;

/// <summary>
/// Factory para herramientas de diseño de EF Core (dotnet ef migrations).
/// Solo se usa en tiempo de diseño — nunca en producción.
/// Lee la connection string desde la variable de entorno ERP_CONNECTION_STRING
/// o desde un fallback local configurado durante desarrollo.
/// </summary>
public sealed class ErpDbContextFactory : IDesignTimeDbContextFactory<ErpDbContext>
{
    public ErpDbContext CreateDbContext(string[] args)
    {
        // Fase 1 Y26: connection string desde variable de entorno para migraciones.
        // Configurar: $env:ERP_CONNECTION_STRING = "Server=...;..."
        var cs = Environment.GetEnvironmentVariable("ERP_CONNECTION_STRING")
            ?? ReadFromAppSettings()
            ?? throw new InvalidOperationException(
                "Design-time connection string no configurada. " +
                "Establece la variable de entorno ERP_CONNECTION_STRING.");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseSqlServer(cs, sql => sql.MigrationsAssembly("Ybridio.Infrastructure"))
            .Options;

        return new ErpDbContext(options, NullSessionContext.Instance);
    }

    private static string? ReadFromAppSettings()
    {
        var root = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                "Ybridio.WinUI"));
        foreach (var name in new[] { "appsettings.Development.json", "appsettings.json" })
        {
            var path = Path.Combine(root, name);
            if (!File.Exists(path)) continue;
            try
            {
                var json = File.ReadAllText(path);
                // Extracción simple sin dependencias extra en Infrastructure
                const string marker = "\"ErpDatabase\":";
                var idx = json.IndexOf(marker, StringComparison.Ordinal);
                if (idx < 0) continue;
                var start = json.IndexOf('"', idx + marker.Length) + 1;
                var end   = json.IndexOf('"', start);
                if (start > 0 && end > start)
                    return json[start..end].Replace("\\\\", "\\");
            }
            catch { /* ignorar, intentar siguiente */ }
        }
        return null;
    }
}

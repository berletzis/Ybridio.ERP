using System.Text.Json;

namespace Ybridio.Infrastructure.Persistence.Audit;

/// <summary>Nivel de severidad de un hallazgo de auditoría de esquema.</summary>
public enum AuditSeverity
{
    /// <summary>Inconsistencia crítica que puede causar corrupción de datos o fallas en runtime (FK faltante, tipo incompatible).</summary>
    Critical = 0,
    /// <summary>Elemento ausente que puede causar errores funcionales (tabla o columna faltante en la BD).</summary>
    Error = 1,
    /// <summary>Elemento no rastreado o potencialmente obsoleto (tabla o columna huérfana en la BD).</summary>
    Warning = 2,
    /// <summary>Estado correcto o información de contexto sin impacto funcional.</summary>
    Info = 3
}

/// <summary>
/// Hallazgo individual de la auditoría de esquema.
/// Inmutable por diseño — el servicio lo crea, la UI lo lee.
/// </summary>
/// <param name="Severity">Nivel de severidad.</param>
/// <param name="Category">Categoría del hallazgo (Migraciones, Tablas, Columnas, Tipos, Relaciones).</param>
/// <param name="Message">Descripción detallada del problema detectado.</param>
/// <param name="Suggestion">Acción sugerida para corregir el problema (opcional).</param>
public sealed record SchemaAuditEntry(
    AuditSeverity Severity,
    string        Category,
    string        Message,
    string?       Suggestion = null);

/// <summary>
/// Reporte agregado producido por <see cref="ISchemaAuditService.RunAsync"/>.
/// Expone contadores por severidad, salida en consola y serialización JSON.
/// </summary>
public sealed class SchemaAuditReport
{
    /// <summary>Momento UTC en que se ejecutó la auditoría.</summary>
    public DateTime ExecutedAt { get; }

    /// <summary>Lista completa de hallazgos, ordenada de mayor a menor severidad.</summary>
    public IReadOnlyList<SchemaAuditEntry> Entries { get; }

    /// <summary>Número de hallazgos de nivel <see cref="AuditSeverity.Critical"/>.</summary>
    public int CriticalCount => Entries.Count(e => e.Severity == AuditSeverity.Critical);

    /// <summary>Número de hallazgos de nivel <see cref="AuditSeverity.Error"/>.</summary>
    public int ErrorCount    => Entries.Count(e => e.Severity == AuditSeverity.Error);

    /// <summary>Número de hallazgos de nivel <see cref="AuditSeverity.Warning"/>.</summary>
    public int WarningCount  => Entries.Count(e => e.Severity == AuditSeverity.Warning);

    /// <summary>Verdadero si existe al menos un hallazgo Critical. Puede usarse para bloquear operaciones sensibles.</summary>
    public bool HasCriticalErrors => CriticalCount > 0;

    /// <summary>Verdadero si existe al menos un hallazgo Critical o Error.</summary>
    public bool HasErrors => CriticalCount > 0 || ErrorCount > 0;

    internal SchemaAuditReport(DateTime executedAt, IReadOnlyList<SchemaAuditEntry> entries)
    {
        ExecutedAt = executedAt;
        Entries    = entries;
    }

    /// <summary>
    /// Imprime el reporte en consola con colores: rojo=Critical, naranja=Error, amarillo=Warning, cyan=Info.
    /// Uso principal: scripts de arranque en modo DEBUG.
    /// </summary>
    public void PrintToConsole()
    {
        Console.WriteLine();
        Console.WriteLine($"══ AUDITORÍA DE ESQUEMA  {ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC ══");
        Console.WriteLine($"   Críticos: {CriticalCount}   Errores: {ErrorCount}   Advertencias: {WarningCount}   Total: {Entries.Count}");
        Console.WriteLine();

        foreach (var entry in Entries)
        {
            var (prefix, color) = entry.Severity switch
            {
                AuditSeverity.Critical => ("[CRITICAL]", ConsoleColor.Red),
                AuditSeverity.Error    => ("[ERROR]   ", ConsoleColor.DarkYellow),
                AuditSeverity.Warning  => ("[WARN]    ", ConsoleColor.Yellow),
                _                      => ("[INFO]    ", ConsoleColor.Cyan)
            };

            Console.ForegroundColor = color;
            Console.Write(prefix);
            Console.ResetColor();
            Console.Write($" [{entry.Category}] {entry.Message}");

            if (entry.Suggestion is not null)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  → {entry.Suggestion}");
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        Console.WriteLine();
        Console.ForegroundColor = HasCriticalErrors ? ConsoleColor.Red
                                : HasErrors         ? ConsoleColor.DarkYellow
                                                    : ConsoleColor.Green;
        Console.WriteLine(HasCriticalErrors
            ? "✗ ERRORES CRÍTICOS detectados. Corregir antes de continuar."
            : HasErrors
                ? "⚠ Errores no críticos detectados. Revisar antes de operaciones sensibles."
                : "✓ Esquema consistente. No se detectaron problemas.");
        Console.ResetColor();
        Console.WriteLine();
    }

    /// <summary>
    /// Serializa el reporte a JSON indentado.
    /// Apto para archivar resultados o integrar con CI/CD.
    /// </summary>
    public string ToJsonString()
    {
        var payload = new
        {
            ExecutedAt,
            Summary = new { CriticalCount, ErrorCount, WarningCount, HasCriticalErrors, HasErrors },
            Entries  = Entries.Select(e => new
            {
                Severity   = e.Severity.ToString(),
                e.Category,
                e.Message,
                e.Suggestion
            })
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}

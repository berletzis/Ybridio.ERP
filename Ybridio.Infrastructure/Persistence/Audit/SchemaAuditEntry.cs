using System.Text.Json;

namespace Ybridio.Infrastructure.Persistence.Audit;

/// <summary>
/// Nivel de severidad de un hallazgo de auditoría estructural.
/// El sistema de auditoría distingue corrupción real de estado legacy esperado y migraciones pendientes.
/// </summary>
public enum AuditSeverity
{
    // ── Problemas reales ────────────────────────────────────────────────────
    /// <summary>
    /// Corrupción estructural real: folio duplicado, FK a entidad inexistente,
    /// total imposible, snapshot corrupto, documento en estado ilegal.
    /// Requiere intervención inmediata.
    /// </summary>
    Critical = 0,

    /// <summary>
    /// Inconsistencia recuperable: tipo de columna incompatible, duplicado funcional,
    /// campo requerido nulo en documento activo.
    /// </summary>
    Error = 1,

    /// <summary>
    /// Divergencia tolerable: tabla en BD sin entidad EF, columna huérfana,
    /// FK legacy apuntando a dbo (estado post-migración esperado).
    /// </summary>
    Warning = 2,

    /// <summary>Estado correcto o información de contexto sin impacto funcional.</summary>
    Info = 3,

    // ── Categorías semánticas ───────────────────────────────────────────────
    /// <summary>
    /// Dato histórico válido generado antes del workflow/arquitectura actual.
    /// Esperado y aceptable — documentos sin folio pre-SerieDocumento, subtotales null legacy.
    /// No requiere acción urgente.
    /// </summary>
    LegacyData = 4,

    /// <summary>
    /// Migración o script manual pendiente de ejecutar.
    /// La columna/tabla/constraint existe en el modelo EF pero no en la BD.
    /// Funcionalidad incompleta hasta que se ejecute el script correspondiente.
    /// </summary>
    MigrationPending = 5,
}

/// <summary>
/// Hallazgo individual de la auditoría ERP.
/// Inmutable por diseño — el servicio lo crea, la UI lo lee.
/// </summary>
/// <param name="Severity">Nivel de severidad.</param>
/// <param name="Category">Categoría técnica del hallazgo (Migraciones, Tablas, Columnas, Lifecycle, Financiero, etc.).</param>
/// <param name="Message">Descripción detallada del problema detectado.</param>
/// <param name="Suggestion">Acción sugerida para corregir el problema (opcional).</param>
/// <param name="Module">Módulo de negocio al que pertenece el hallazgo (Cotizaciones, Pedidos, Ventas, Catálogos, Esquema, etc.). Null para hallazgos transversales.</param>
public sealed record SchemaAuditEntry(
    AuditSeverity Severity,
    string        Category,
    string        Message,
    string?       Suggestion = null,
    string?       Module     = null);

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

    /// <summary>Número de hallazgos <see cref="AuditSeverity.Critical"/> — corrupción real.</summary>
    public int CriticalCount        => Entries.Count(e => e.Severity == AuditSeverity.Critical);

    /// <summary>Número de hallazgos <see cref="AuditSeverity.Error"/> — inconsistencia recuperable.</summary>
    public int ErrorCount           => Entries.Count(e => e.Severity == AuditSeverity.Error);

    /// <summary>Número de hallazgos <see cref="AuditSeverity.Warning"/> — divergencia tolerable.</summary>
    public int WarningCount         => Entries.Count(e => e.Severity == AuditSeverity.Warning);

    /// <summary>Número de hallazgos <see cref="AuditSeverity.LegacyData"/> — datos históricos válidos.</summary>
    public int LegacyDataCount      => Entries.Count(e => e.Severity == AuditSeverity.LegacyData);

    /// <summary>Número de hallazgos <see cref="AuditSeverity.MigrationPending"/> — scripts pendientes.</summary>
    public int MigrationPendingCount => Entries.Count(e => e.Severity == AuditSeverity.MigrationPending);

    /// <summary>Verdadero si existe al menos un hallazgo Critical.</summary>
    public bool HasCriticalErrors => CriticalCount > 0;

    /// <summary>Verdadero si existe al menos un hallazgo Critical o Error.</summary>
    public bool HasErrors => CriticalCount > 0 || ErrorCount > 0;

    /// <summary>Verdadero si existen scripts manuales pendientes de ejecutar.</summary>
    public bool HasPendingMigrations => MigrationPendingCount > 0;

    internal SchemaAuditReport(DateTime executedAt, IReadOnlyList<SchemaAuditEntry> entries)
    {
        ExecutedAt = executedAt;
        Entries    = entries;
    }

    /// <summary>
    /// Desglose de hallazgos por módulo de negocio.
    /// Clave: nombre del módulo (o "Sin módulo" si Module es null).
    /// Valor: conteo de hallazgos con severidad Critical o Error para ese módulo.
    /// </summary>
    public IReadOnlyDictionary<string, int> GetModuleBreakdown()
        => Entries
            .Where(e => e.Severity is AuditSeverity.Critical or AuditSeverity.Error)
            .GroupBy(e => e.Module ?? "General")
            .ToDictionary(g => g.Key, g => g.Count());

    /// <summary>
    /// Todos los módulos presentes en este reporte (con y sin errores).
    /// </summary>
    public IReadOnlyList<string> GetModules()
        => Entries
            .Select(e => e.Module ?? "General")
            .Distinct()
            .OrderBy(m => m)
            .ToList();

    /// <summary>
    /// Imprime el reporte en consola con colores: rojo=Critical, naranja=Error, amarillo=Warning, cyan=Info.
    /// Uso principal: scripts de arranque en modo DEBUG.
    /// </summary>
    public void PrintToConsole()
    {
        Console.WriteLine();
        Console.WriteLine($"══ AUDITORÍA ERP  {ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC ══");
        Console.WriteLine($"   Críticos: {CriticalCount}   Errores: {ErrorCount}   Advertencias: {WarningCount}   Legacy: {LegacyDataCount}   Migr.Pend.: {MigrationPendingCount}   Total: {Entries.Count}");
        Console.WriteLine();

        foreach (var entry in Entries)
        {
            var (prefix, color) = entry.Severity switch
            {
                AuditSeverity.Critical         => ("[CRITICAL]  ", ConsoleColor.Red),
                AuditSeverity.Error            => ("[ERROR]     ", ConsoleColor.DarkYellow),
                AuditSeverity.Warning          => ("[WARN]      ", ConsoleColor.Yellow),
                AuditSeverity.LegacyData       => ("[LEGACY]    ", ConsoleColor.DarkCyan),
                AuditSeverity.MigrationPending => ("[MIGR.PEND] ", ConsoleColor.Magenta),
                _                              => ("[INFO]      ", ConsoleColor.Cyan)
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
            Summary = new { CriticalCount, ErrorCount, WarningCount, LegacyDataCount, MigrationPendingCount, HasCriticalErrors, HasErrors, HasPendingMigrations },
            Entries  = Entries.Select(e => new
            {
                Severity   = e.Severity.ToString(),
                e.Module,
                e.Category,
                e.Message,
                e.Suggestion
            })
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}

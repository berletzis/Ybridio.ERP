using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Ybridio.Infrastructure.Persistence.Audit;

/// <summary>
/// Implementación de <see cref="ISchemaAuditService"/> para SQL Server.
/// Obtiene el modelo esperado de <see cref="ErpDbContext.Model"/> y el esquema real
/// de INFORMATION_SCHEMA, luego compara ambos e informa divergencias.
/// </summary>
/// <remarks>
/// Registro sugerido en DI (solo para ambientes de desarrollo):
/// <code>
/// #if DEBUG
///   services.AddScoped&lt;ISchemaAuditService, SchemaAuditService&gt;();
/// #endif
/// </code>
/// </remarks>
public sealed class SchemaAuditService : ISchemaAuditService
{
    private readonly ErpDbContext _db;

    /// <summary>Tablas del sistema que el auditor ignora al buscar tablas huérfanas.</summary>
    private static readonly HashSet<string> SystemTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "__EFMigrationsHistory",
        "sysdiagrams"
    };

    public SchemaAuditService(ErpDbContext db) => _db = db;

    // ── Punto de entrada ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<SchemaAuditReport> RunAsync(CancellationToken ct = default)
    {
        var findings = new List<SchemaAuditEntry>();

        try
        {
            await AuditPendingMigrationsAsync(findings, ct);
            var dbSchema = await LoadDatabaseSchemaAsync(ct);
            AuditTables(findings, dbSchema);
            AuditColumns(findings, dbSchema);
            AuditForeignKeys(findings, dbSchema);
        }
        catch (Exception ex)
        {
            findings.Add(new SchemaAuditEntry(
                AuditSeverity.Critical, "Conexión",
                $"Error inesperado durante la auditoría: {ex.Message}",
                "Verificar que la base de datos esté accesible y la cadena de conexión sea correcta."));
        }

        // Ordenar: Errors primero, luego Warnings, luego Info
        findings.Sort((a, b) => a.Severity.CompareTo(b.Severity));

        return new SchemaAuditReport(DateTime.UtcNow, findings.AsReadOnly());
    }

    // ── 1. Migraciones pendientes ───────────────────────────────────────────

    private async Task AuditPendingMigrationsAsync(List<SchemaAuditEntry> findings, CancellationToken ct)
    {
        var pending = (await _db.Database.GetPendingMigrationsAsync(ct)).ToList();

        if (pending.Count == 0)
        {
            findings.Add(new SchemaAuditEntry(
                AuditSeverity.Info, "Migraciones",
                "Todas las migraciones están aplicadas en la base de datos."));
            return;
        }

        foreach (var migration in pending)
        {
            findings.Add(new SchemaAuditEntry(
                AuditSeverity.Error, "Migraciones",
                $"Migración pendiente: {migration}",
                "Ejecutar: dotnet ef database update --project Ybridio.Infrastructure --startup-project Ybridio.WinUI"));
        }
    }

    // ── 2. Carga del esquema real desde INFORMATION_SCHEMA ─────────────────

    /// <summary>Snapshot del esquema de la BD real.</summary>
    private sealed record DatabaseSchema(
        /// <summary>Claves "schema.tabla" de todas las tablas existentes en la BD.</summary>
        HashSet<string> Tables,
        /// <summary>Columnas por tabla: clave "schema.tabla" → nombre columna → tipo SQL base.</summary>
        Dictionary<string, Dictionary<string, string>> Columns,
        /// <summary>Nombres de restricciones FOREIGN KEY presentes en la BD.</summary>
        HashSet<string> ForeignKeyNames);

    private async Task<DatabaseSchema> LoadDatabaseSchemaAsync(CancellationToken ct)
    {
        var tables    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var columns   = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var fkNames   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var connection = _db.Database.GetDbConnection();
        var needsOpen  = connection.State != ConnectionState.Open;
        if (needsOpen) await connection.OpenAsync(ct);

        try
        {
            // ── Tablas ────────────────────────────────────────────────────
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES " +
                    "WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME";

                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var key = TableKey(reader.GetString(0), reader.GetString(1));
                    tables.Add(key);
                    columns[key] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
            }

            // ── Columnas ──────────────────────────────────────────────────
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE " +
                    "FROM INFORMATION_SCHEMA.COLUMNS " +
                    "ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION";

                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var tableKey = TableKey(reader.GetString(0), reader.GetString(1));
                    if (columns.TryGetValue(tableKey, out var colDict))
                        colDict[reader.GetString(2)] = reader.GetString(3); // column → base type
                }
            }

            // ── Foreign Keys ──────────────────────────────────────────────
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS " +
                    "WHERE CONSTRAINT_TYPE = 'FOREIGN KEY'";

                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    fkNames.Add(reader.GetString(0));
            }
        }
        finally
        {
            if (needsOpen) await connection.CloseAsync();
        }

        return new DatabaseSchema(tables, columns, fkNames);
    }

    // ── 3. Comparación de tablas ────────────────────────────────────────────

    private void AuditTables(List<SchemaAuditEntry> findings, DatabaseSchema dbSchema)
    {
        var efTableKeys = GetEfTableKeys();

        // Tablas en EF que no están en la BD
        foreach (var (key, schema, table) in efTableKeys)
        {
            if (!dbSchema.Tables.Contains(key))
            {
                findings.Add(new SchemaAuditEntry(
                    AuditSeverity.Error, "Tablas",
                    $"[{schema}].[{table}] definida en el modelo EF no existe en la base de datos.",
                    $"Generar migración: dotnet ef migrations add Add{table}"));
            }
        }

        // Tablas en la BD que no tienen entidad en EF
        var efKeys = efTableKeys.Select(t => t.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var dbKey in dbSchema.Tables)
        {
            var tableName = dbKey.Split('.').Last();
            if (SystemTables.Contains(tableName)) continue;
            if (!efKeys.Contains(dbKey))
            {
                var parts = dbKey.Split('.', 2);
                findings.Add(new SchemaAuditEntry(
                    AuditSeverity.Warning, "Tablas",
                    $"[{parts[0]}].[{parts[1]}] existe en la base de datos pero no tiene entidad en el modelo EF.",
                    "Verificar si es una tabla legacy o huérfana. Considerar agregarla al modelo o eliminarla."));
            }
        }
    }

    // ── 4. Comparación de columnas ──────────────────────────────────────────

    private void AuditColumns(List<SchemaAuditEntry> findings, DatabaseSchema dbSchema)
    {
        foreach (var entityType in GetMappedEntityTypes())
        {
            var tableName = entityType.GetTableName()!;
            var schema    = entityType.GetSchema() ?? "dbo";
            var tableKey  = TableKey(schema, tableName);

            if (!dbSchema.Columns.TryGetValue(tableKey, out var dbCols)) continue; // tabla ya reportada como faltante

            var storeObj   = StoreObjectIdentifier.Table(tableName, schema);
            var efColNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in entityType.GetProperties())
            {
                var colName = property.GetColumnName(storeObj);
                if (colName is null) continue; // propiedad no mapeada a esta tabla

                efColNames.Add(colName);

                if (!dbCols.TryGetValue(colName, out var dbBaseType))
                {
                    // Columna en EF no existe en BD
                    findings.Add(new SchemaAuditEntry(
                        AuditSeverity.Error, "Columnas",
                        $"[{schema}].[{tableName}].[{colName}] existe en el modelo EF pero NO en la base de datos.",
                        "Generar una migración para agregar la columna."));
                }
                else
                {
                    // Verificar compatibilidad de tipo
                    var efType     = property.GetColumnType();
                    var efBaseType = ExtractBaseType(efType);

                    if (!TypesCompatible(efBaseType, dbBaseType))
                    {
                        // Tipo incompatible = Critical: puede causar errores de conversión en runtime
                        findings.Add(new SchemaAuditEntry(
                            AuditSeverity.Critical, "Tipos",
                            $"[{schema}].[{tableName}].[{colName}]: EF espera '{efType}' pero la BD tiene '{dbBaseType}'.",
                            "Revisar la configuración EF o generar una migración de alteración de columna."));
                    }
                }
            }

            // Columnas en BD que no tienen propiedad en EF
            foreach (var dbColName in dbCols.Keys)
            {
                if (!efColNames.Contains(dbColName))
                {
                    findings.Add(new SchemaAuditEntry(
                        AuditSeverity.Warning, "Columnas",
                        $"[{schema}].[{tableName}].[{dbColName}] existe en la BD pero no tiene propiedad en el modelo EF.",
                        "Verificar si es una columna legacy. Considerar mapearla o eliminarla con una migración."));
                }
            }
        }
    }

    // ── 5. Comparación de Foreign Keys ─────────────────────────────────────

    private void AuditForeignKeys(List<SchemaAuditEntry> findings, DatabaseSchema dbSchema)
    {
        foreach (var entityType in GetMappedEntityTypes())
        {
            foreach (var fk in entityType.GetForeignKeys())
            {
                var constraintName = fk.GetConstraintName();
                if (constraintName is null) continue;

                if (!dbSchema.ForeignKeyNames.Contains(constraintName))
                {
                    var depTable  = entityType.GetTableName();
                    var depSchema = entityType.GetSchema() ?? "dbo";
                    var refTable  = fk.PrincipalEntityType.GetTableName();

                    // FK faltante = Critical: puede permitir registros huérfanos e inconsistencias de datos
                    findings.Add(new SchemaAuditEntry(
                        AuditSeverity.Critical, "Relaciones",
                        $"FK '{constraintName}' ({depSchema}.{depTable} → {refTable}) existe en el modelo EF pero NO en la base de datos.",
                        "Generar una migración para agregar la restricción de clave foránea."));
                }
            }
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Retorna todos los tipos de entidad que tienen una tabla asignada (excluye owned types sin tabla propia).
    /// Deduplica por tabla para evitar reportar la misma tabla múltiples veces en jerarquías TPH.
    /// </summary>
    private IEnumerable<IEntityType> GetMappedEntityTypes()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return _db.Model.GetEntityTypes()
            .Where(e => e.GetTableName() is not null)
            .Where(e => seen.Add(TableKey(e.GetSchema() ?? "dbo", e.GetTableName()!)));
    }

    /// <summary>
    /// Retorna las claves de tabla únicas (clave, schema, nombre) del modelo EF.
    /// </summary>
    private List<(string Key, string Schema, string Table)> GetEfTableKeys()
    {
        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(string, string, string)>();

        foreach (var et in _db.Model.GetEntityTypes())
        {
            var table  = et.GetTableName();
            if (table is null) continue;
            var schema = et.GetSchema() ?? "dbo";
            var key    = TableKey(schema, table);
            if (seen.Add(key)) result.Add((key, schema, table));
        }

        return result;
    }

    /// <summary>Construye una clave normalizada "schema.tabla" en minúsculas.</summary>
    private static string TableKey(string schema, string table)
        => $"{schema.ToLowerInvariant()}.{table.ToLowerInvariant()}";

    /// <summary>
    /// Extrae el tipo base de un tipo EF como "nvarchar(256)" → "nvarchar"
    /// o "decimal(18,6)" → "decimal".
    /// </summary>
    private static string ExtractBaseType(string efColumnType)
    {
        var paren = efColumnType.IndexOf('(');
        return (paren >= 0 ? efColumnType[..paren] : efColumnType).ToLowerInvariant().Trim();
    }

    /// <summary>
    /// Comprueba si el tipo EF base y el tipo INFORMATION_SCHEMA son compatibles.
    /// Maneja alias SQL Server: rowversion=timestamp, numeric=decimal, etc.
    /// </summary>
    private static bool TypesCompatible(string efBase, string dbBase)
    {
        if (string.Equals(efBase, dbBase, StringComparison.OrdinalIgnoreCase)) return true;

        // Alias conocidos de SQL Server
        return (efBase, dbBase.ToLowerInvariant()) switch
        {
            ("rowversion",  "timestamp")  => true,
            ("timestamp",   "rowversion") => true,
            ("numeric",     "decimal")    => true,
            ("decimal",     "numeric")    => true,
            ("nvarchar",    "sysname")    => true,  // sysname es alias de nvarchar(128)
            ("int",         "int")        => true,
            _ => false
        };
    }
}

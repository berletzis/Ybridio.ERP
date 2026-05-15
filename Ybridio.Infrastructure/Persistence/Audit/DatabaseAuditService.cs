using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Ybridio.Infrastructure.Persistence.Audit;

/// <summary>
/// Auditoría de integridad de datos post-migración dbo → catalogos.
/// Cada validación es un método privado independiente que ejecuta
/// una sola query SELECT y devuelve sus propios hallazgos.
/// Nunca modifica datos ni ejecuta batches SQL completos.
/// </summary>
public sealed class DatabaseAuditService : IDatabaseAuditService
{
    private readonly ErpDbContext _db;

    public DatabaseAuditService(ErpDbContext db) => _db = db;

    // ── Punto de entrada ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<SchemaAuditReport> RunAsync(CancellationToken ct = default)
    {
        var findings = new List<SchemaAuditEntry>();

        try
        {
            var conn = _db.Database.GetDbConnection();
            var needsOpen = conn.State != ConnectionState.Open;
            if (needsOpen) await conn.OpenAsync(ct);

            try
            {
                // Orden: errores críticos primero, luego consistencia, luego estado
                findings.AddRange(await GetInvalidForeignKeysAsync(conn, ct));
                findings.AddRange(await GetOrphanRecordsAsync(conn, ct));
                findings.AddRange(await GetLegacyDependenciesAsync(conn, ct));
                findings.AddRange(await GetUnmigratedRecordsAsync(conn, ct));
                findings.AddRange(await GetDuplicateCatalogsAsync(conn, ct));
                findings.AddRange(await GetDataIssuesAsync(conn, ct));
            }
            finally
            {
                if (needsOpen) await conn.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            findings.Add(new SchemaAuditEntry(
                AuditSeverity.Critical, "Conexión",
                $"Error al ejecutar la auditoría de datos: {ex.Message}",
                "Verificar conectividad y permisos sobre schemas catalogos y migmap."));
        }

        findings.Sort((a, b) => a.Severity.CompareTo(b.Severity));
        return new SchemaAuditReport(DateTime.UtcNow, findings.AsReadOnly());
    }

    // ════════════════════════════════════════════════════════════════════════
    // GetInvalidForeignKeys — query independiente por cada FK en Producto
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Valida cada FK de <c>catalogos.Producto</c> con una query individual.
    /// Detecta referencias a registros inexistentes o borrados en los catálogos.
    /// Severidad: <see cref="AuditSeverity.Critical"/> si hay rotos.
    /// </summary>
    private async Task<IReadOnlyList<SchemaAuditEntry>> GetInvalidForeignKeysAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // FK 1 — TipoProductoId (Produto está en core, TipoProducto en catalogos)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM core.Producto p
            WHERE p.TipoProductoId IS NOT NULL
              AND p.Borrado = 0
              AND NOT EXISTS (
                  SELECT 1 FROM catalogos.TipoProducto t
                  WHERE t.Id = p.TipoProductoId AND t.Borrado = 0
              )";
        var tipoRoto = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(tipoRoto > 0
            ? new SchemaAuditEntry(AuditSeverity.Critical, "FK Producto",
                $"TipoProductoId: {tipoRoto} producto(s) apuntan a un TipoProducto inexistente o borrado.",
                "Reasignar TipoProductoId a un Id válido en catalogos.TipoProducto.")
            : new SchemaAuditEntry(AuditSeverity.Info, "FK Producto",
                "TipoProductoId: todas las referencias son válidas."));

        // FK 2 — UnidadMedidaId
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM core.Producto p
            WHERE p.UnidadMedidaId IS NOT NULL
              AND p.Borrado = 0
              AND NOT EXISTS (
                  SELECT 1 FROM catalogos.UnidadMedida u
                  WHERE u.Id = p.UnidadMedidaId AND u.Borrado = 0
              )";
        var unidadRota = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(unidadRota > 0
            ? new SchemaAuditEntry(AuditSeverity.Critical, "FK Producto",
                $"UnidadMedidaId: {unidadRota} producto(s) apuntan a una UnidadMedida inexistente o borrada.",
                "Reasignar UnidadMedidaId a un Id válido en catalogos.UnidadMedida.")
            : new SchemaAuditEntry(AuditSeverity.Info, "FK Producto",
                "UnidadMedidaId: todas las referencias son válidas."));

        // FK 3 — TipoImpuestoId
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM core.Producto p
            WHERE p.TipoImpuestoId IS NOT NULL
              AND p.Borrado = 0
              AND NOT EXISTS (
                  SELECT 1 FROM catalogos.TipoImpuesto i
                  WHERE i.Id = p.TipoImpuestoId AND i.Borrado = 0
              )";
        var impuestoRoto = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(impuestoRoto > 0
            ? new SchemaAuditEntry(AuditSeverity.Critical, "FK Producto",
                $"TipoImpuestoId: {impuestoRoto} producto(s) apuntan a un TipoImpuesto inexistente o borrado.",
                "Reasignar TipoImpuestoId a un Id válido en catalogos.TipoImpuesto.")
            : new SchemaAuditEntry(AuditSeverity.Info, "FK Producto",
                "TipoImpuestoId: todas las referencias son válidas."));

        return results;
    }

    // ════════════════════════════════════════════════════════════════════════
    // GetOrphanRecords — integridad de tablas migmap
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifica que cada registro en las tablas <c>migmap.*</c> tenga un
    /// <c>IdNuevo</c> que exista en la tabla <c>catalogos.*</c> correspondiente.
    /// Una query por tabla de mapa.
    /// </summary>
    private async Task<IReadOnlyList<SchemaAuditEntry>> GetOrphanRecordsAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // migmap eliminado 2026-05-14 — schema nunca usado (13 tablas vacías)
        cmd.CommandText = "SELECT COUNT(*) FROM sys.schemas WHERE name = 'migmap'";
        if ((int)(await cmd.ExecuteScalarAsync(ct) ?? 0) == 0)
        {
            results.Add(new SchemaAuditEntry(AuditSeverity.Info, "Migmap",
                "Schema migmap no existe — eliminado correctamente (era infraestructura vacía).", null));
            return results;
        }

        // Tabla de mapas con su tabla destino
        var maps = new (string MapTable, string CatalogTable)[]
        {
            ("TipoProducto_Map",      "catalogos.TipoProducto"),
            ("UnidadMedida_Map",      "catalogos.UnidadMedida"),
            ("TipoImpuesto_Map",      "catalogos.TipoImpuesto"),
            ("CategoriaProducto_Map", "catalogos.CategoriaProducto"),
            ("Moneda_Map",            "catalogos.Moneda"),
            ("FormaPago_Map",         "catalogos.FormaPago"),
            ("Pais_Map",              "catalogos.Pais"),
            ("MetodoPago_Map",        "catalogos.MetodoPago"),
        };

        foreach (var (mapTable, catalogTable) in maps)
        {
            // Query 1: verificar existencia de la tabla migmap
            cmd.CommandText = @"
                SELECT COUNT(*) FROM sys.objects o
                JOIN sys.schemas s ON o.schema_id = s.schema_id
                WHERE s.name = 'migmap' AND o.name = @tableName";
            cmd.Parameters.Clear();
            var p = cmd.CreateParameter();
            p.ParameterName = "@tableName";
            p.Value = mapTable;
            cmd.Parameters.Add(p);

            var tableExists = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0) > 0;
            cmd.Parameters.Clear();

            if (!tableExists)
            {
                results.Add(new SchemaAuditEntry(AuditSeverity.Info, "Registros huérfanos",
                    $"migmap.{mapTable} no existe. Catálogo no fue migrado desde dbo."));
                continue;
            }

            // Query 2: contar entradas de migmap cuyo IdNuevo no existe en catalogos
            cmd.CommandText = $@"
                SELECT COUNT(*)
                FROM migmap.[{mapTable}] m
                WHERE NOT EXISTS (
                    SELECT 1 FROM {catalogTable} c WHERE c.Id = m.IdNuevo
                )";
            var orphans = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
            results.Add(orphans > 0
                ? new SchemaAuditEntry(AuditSeverity.Error, "Registros huérfanos",
                    $"migmap.{mapTable}: {orphans} entrada(s) cuyo IdNuevo ya no existe en {catalogTable}.",
                    $"Los registros destino en {catalogTable} fueron eliminados. Revisar o limpiar el mapa.")
                : new SchemaAuditEntry(AuditSeverity.Info, "Registros huérfanos",
                    $"migmap.{mapTable}: sin registros huérfanos."));
        }

        return results;
    }

    // ════════════════════════════════════════════════════════════════════════
    // GetLegacyDependencies — FK constraints apuntando a dbo
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Detecta FK constraints definidas en schemas del ERP que aún referencian
    /// tablas del schema <c>dbo</c> legacy.
    /// Usa <c>sys.foreign_keys</c> — no requiere que las tablas dbo existan.
    /// </summary>
    private async Task<IReadOnlyList<SchemaAuditEntry>> GetLegacyDependenciesAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // Query 1: FK constraints de schemas ERP → dbo
        cmd.CommandText = @"
            SELECT
                SCHEMA_NAME(tp.schema_id) + '.' + tp.name AS TablaOrigen,
                fk.name                                   AS NombreFK,
                rp.name                                   AS TablaDestino
            FROM sys.foreign_keys fk
            JOIN sys.tables tp ON fk.parent_object_id     = tp.object_id
            JOIN sys.tables rp ON fk.referenced_object_id = rp.object_id
            WHERE SCHEMA_NAME(rp.schema_id) = 'dbo'
              AND SCHEMA_NAME(tp.schema_id) IN (
                  'catalogos','inventario','finanzas','ventas',
                  'compras','seguridad','core'
              )
            ORDER BY TablaOrigen";

        using var r = await cmd.ExecuteReaderAsync(ct);
        var depsCount = 0;
        while (await r.ReadAsync(ct))
        {
            depsCount++;
            // FK a dbo → Warning, no Critical.
            // Es el estado ESPERADO post-migración en un ERP que usa scripts manuales.
            // Solo es Critical si hay datos con referencias rotas (validado en WorkflowAuditService).
            results.Add(new SchemaAuditEntry(AuditSeverity.Warning, "Dependencias dbo",
                $"FK residual: {r.GetString(0)} → dbo.{r.GetString(2)} (constraint: {r.GetString(1)})",
                "Estado post-migración esperado. Actualizar FK a catalogos.* cuando sea operacionalmente seguro."));
        }
        await r.CloseAsync();

        if (depsCount == 0)
            results.Add(new SchemaAuditEntry(AuditSeverity.Info, "Dependencias dbo",
                "Sin FK constraints de schemas ERP apuntando a dbo."));

        // Query 2: conteo de tablas dbo legacy aún presentes
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM sys.tables t
            JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = 'dbo'
              AND t.name IN (
                  'TipoDeProducto','UnidadDeMedida','Categoria',
                  'CatSAT_Impuesto','CatSAT_Moneda','CatSAT_FormaPago',
                  'MetodoDePago','Pais','Estado'
              )";
        var legacyCount = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(new SchemaAuditEntry(AuditSeverity.Info, "Tablas legacy",
            $"{legacyCount} tabla(s) dbo de catálogos legacy presentes en la BD " +
            "(no eliminadas por diseño — conservadas para compatibilidad)."));

        return results;
    }

    // ════════════════════════════════════════════════════════════════════════
    // GetUnmigratedRecords — registros en dbo sin equivalente en migmap
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Para cada par (tabla dbo, tabla migmap), verifica primero si ambas
    /// existen y luego cuenta registros dbo no presentes en el mapa.
    /// Dos queries independientes por tabla: una de existencia, una de conteo.
    /// </summary>
    private async Task<IReadOnlyList<SchemaAuditEntry>> GetUnmigratedRecordsAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var existCmd = conn.CreateCommand();
        using var countCmd = conn.CreateCommand();

        // migmap eliminado 2026-05-14 — schema no existe, validación N/A
        existCmd.CommandText = "SELECT COUNT(*) FROM sys.schemas WHERE name = 'migmap'";
        if ((int)(await existCmd.ExecuteScalarAsync(ct) ?? 0) == 0)
        {
            results.Add(new SchemaAuditEntry(AuditSeverity.Info, "Migmap",
                "Schema migmap no existe — eliminado correctamente.", null));
            return results;
        }

        // Pares (schema dbo, tabla dbo, PK legacy, tabla migmap, tabla catalogos)
        var pairs = new (string DboTable, string PkCol, string MapTable, string CatalogLabel)[]
        {
            ("TipoDeProducto",  "Sequence", "TipoProducto_Map",      "catalogos.TipoProducto"),
            ("UnidadDeMedida",  "Sequence", "UnidadMedida_Map",       "catalogos.UnidadMedida"),
            ("Categoria",       "Sequence", "CategoriaProducto_Map",  "catalogos.CategoriaProducto"),
            ("CatSAT_Impuesto", "Sequence", "TipoImpuesto_Map",       "catalogos.TipoImpuesto"),
            ("CatSAT_Moneda",   "Sequence", "Moneda_Map",             "catalogos.Moneda"),
            ("CatSAT_FormaPago","Sequence", "FormaPago_Map",          "catalogos.FormaPago"),
            ("Pais",            "Sequence", "Pais_Map",               "catalogos.Pais"),
            ("MetodoDePago",    "Sequence", "MetodoPago_Map",         "catalogos.MetodoPago"),
        };

        foreach (var (dboTable, pkCol, mapTable, catalogLabel) in pairs)
        {
            // Query 1: ¿existe la tabla dbo?
            existCmd.CommandText = @"
                SELECT COUNT(*) FROM sys.tables t
                JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = 'dbo' AND t.name = @tableName";
            existCmd.Parameters.Clear();
            var ep = existCmd.CreateParameter();
            ep.ParameterName = "@tableName";
            ep.Value = dboTable;
            existCmd.Parameters.Add(ep);

            var dboExists = (int)(await existCmd.ExecuteScalarAsync(ct) ?? 0) > 0;
            existCmd.Parameters.Clear();

            if (!dboExists)
            {
                results.Add(new SchemaAuditEntry(AuditSeverity.Info, "Sin migrar",
                    $"dbo.{dboTable}: tabla no existe en esta BD. Migración N/A."));
                continue;
            }

            // Query 2: ¿existe la tabla migmap?
            existCmd.CommandText = @"
                SELECT COUNT(*) FROM sys.objects o
                JOIN sys.schemas s ON o.schema_id = s.schema_id
                WHERE s.name = 'migmap' AND o.name = @mapName";
            var mp = existCmd.CreateParameter();
            mp.ParameterName = "@mapName";
            mp.Value = mapTable;
            existCmd.Parameters.Add(mp);

            var mapExists = (int)(await existCmd.ExecuteScalarAsync(ct) ?? 0) > 0;
            existCmd.Parameters.Clear();

            if (!mapExists)
            {
                results.Add(new SchemaAuditEntry(AuditSeverity.Warning, "Sin migrar",
                    $"dbo.{dboTable} existe pero migmap.{mapTable} no. Catálogo no fue migrado.",
                    "Re-ejecutar la sección D correspondiente del script migracion_catalogos.sql."));
                continue;
            }

            // Query 3: contar registros de dbo sin entrada en migmap
            // Seguro: ambas tablas se verificaron arriba antes de referenciarlas
            countCmd.CommandText = $@"
                SELECT COUNT(*)
                FROM dbo.[{dboTable}] d
                WHERE NOT EXISTS (
                    SELECT 1 FROM migmap.[{mapTable}] m
                    WHERE m.IdLegacy = d.[{pkCol}]
                )";
            var unmigratedCount = (int)(await countCmd.ExecuteScalarAsync(ct) ?? 0);

            results.Add(unmigratedCount > 0
                ? new SchemaAuditEntry(AuditSeverity.Error, "Sin migrar",
                    $"dbo.{dboTable}: {unmigratedCount} registro(s) no migrado(s) a {catalogLabel}.",
                    "Re-ejecutar la sección de migración correspondiente del script migracion_catalogos.sql.")
                : new SchemaAuditEntry(AuditSeverity.Info, "Sin migrar",
                    $"dbo.{dboTable}: todos los registros migrados a {catalogLabel}."));
        }

        return results;
    }

    // ════════════════════════════════════════════════════════════════════════
    // GetDuplicateCatalogs — query individual por tabla
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Detecta registros con el mismo <c>Nombre</c> o <c>Clave</c> dentro del
    /// mismo <c>EmpresaId</c> en catálogos por empresa, o globalmente en
    /// catálogos sin empresa. Una query por tabla.
    /// </summary>
    private async Task<IReadOnlyList<SchemaAuditEntry>> GetDuplicateCatalogsAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // Catálogos por empresa: duplicado = mismo Nombre + mismo EmpresaId
        var byEmpresa = new[]
        {
            "catalogos.TipoProducto",
            "catalogos.UnidadMedida",
            "catalogos.TipoImpuesto",
            "catalogos.CategoriaProducto",
        };

        foreach (var tabla in byEmpresa)
        {
            cmd.CommandText = $@"
                SELECT COUNT(*)
                FROM (
                    SELECT Nombre, EmpresaId
                    FROM {tabla}
                    WHERE Borrado = 0
                    GROUP BY Nombre, EmpresaId
                    HAVING COUNT(*) > 1
                ) duplicados";
            var count = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
            results.Add(count > 0
                ? new SchemaAuditEntry(AuditSeverity.Error, "Duplicados",
                    $"{tabla}: {count} grupo(s) con el mismo Nombre por empresa.",
                    $"Revisar y marcar Borrado=1 en los duplicados de {tabla}.")
                : new SchemaAuditEntry(AuditSeverity.Info, "Duplicados",
                    $"{tabla}: sin duplicados por empresa."));
        }

        // Catálogos globales: duplicado = misma Clave o Nombre (sin EmpresaId)
        var globalPorClave = new[] { "catalogos.Moneda", "catalogos.FormaPago" };
        foreach (var tabla in globalPorClave)
        {
            cmd.CommandText = $@"
                SELECT COUNT(*)
                FROM (
                    SELECT Clave FROM {tabla}
                    WHERE Borrado = 0
                    GROUP BY Clave
                    HAVING COUNT(*) > 1
                ) duplicados";
            var count = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
            results.Add(count > 0
                ? new SchemaAuditEntry(AuditSeverity.Error, "Duplicados",
                    $"{tabla}: {count} clave(s) duplicada(s).",
                    $"Revisar y eliminar duplicados en {tabla}.")
                : new SchemaAuditEntry(AuditSeverity.Info, "Duplicados",
                    $"{tabla}: sin Claves duplicadas."));
        }

        // Pais: duplicado = mismo Nombre global
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM (
                SELECT Nombre FROM catalogos.Pais
                WHERE Borrado = 0
                GROUP BY Nombre
                HAVING COUNT(*) > 1
            ) duplicados";
        var paisDups = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(paisDups > 0
            ? new SchemaAuditEntry(AuditSeverity.Error, "Duplicados",
                $"catalogos.Pais: {paisDups} nombre(s) de país duplicado(s).",
                "Revisar y eliminar registros duplicados en catalogos.Pais.")
            : new SchemaAuditEntry(AuditSeverity.Info, "Duplicados",
                "catalogos.Pais: sin duplicados."));

        return results;
    }

    // ════════════════════════════════════════════════════════════════════════
    // GetDataIssues — coordinador de validaciones específicas
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Agrega los resultados de todas las validaciones de datos específicas.
    /// Cada subfunción ejecuta queries independientes y devuelve sus hallazgos.
    /// </summary>
    private async Task<IReadOnlyList<SchemaAuditEntry>> GetDataIssuesAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        results.AddRange(await ValidateCategoryHierarchyAsync(conn, ct));
        results.AddRange(await ValidateImpuestoPercentageAsync(conn, ct));
        results.AddRange(await ValidateAbreviaturaTruncationAsync(conn, ct));
        results.AddRange(await ValidateGeographyAsync(conn, ct));
        results.AddRange(await ValidateNullRequiredFieldsAsync(conn, ct));
        results.AddRange(await ValidateCatalogCountsAsync(conn, ct));
        return results;
    }

    // ── ValidateCategoryHierarchy ──────────────────────────────────────────

    /// <summary>
    /// Dos queries: padre inexistente/borrado y ciclo directo (Id = CategoriaPadreId).
    /// </summary>
    private async Task<IReadOnlyList<SchemaAuditEntry>> ValidateCategoryHierarchyAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // Query 1: padre inválido o borrado
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM catalogos.CategoriaProducto c
            WHERE c.CategoriaPadreId IS NOT NULL
              AND c.Borrado = 0
              AND NOT EXISTS (
                  SELECT 1 FROM catalogos.CategoriaProducto padre
                  WHERE padre.Id = c.CategoriaPadreId AND padre.Borrado = 0
              )";
        var broken = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(broken > 0
            ? new SchemaAuditEntry(AuditSeverity.Error, "Jerarquía",
                $"{broken} CategoriaProducto(s) con CategoriaPadreId apuntando a un padre borrado o inexistente.",
                "Establecer CategoriaPadreId = NULL o corregir la referencia al padre.")
            : new SchemaAuditEntry(AuditSeverity.Info, "Jerarquía",
                "CategoriaProducto: jerarquía de padres válida."));

        // Query 2: ciclo directo (la categoría es su propio padre)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM catalogos.CategoriaProducto
            WHERE Id = CategoriaPadreId";
        var cycles = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (cycles > 0)
            results.Add(new SchemaAuditEntry(AuditSeverity.Critical, "Jerarquía",
                $"{cycles} CategoriaProducto(s) con CategoriaPadreId = su propio Id (ciclo directo).",
                "UPDATE catalogos.CategoriaProducto SET CategoriaPadreId = NULL WHERE Id = CategoriaPadreId"));

        return results;
    }

    // ── ValidateImpuestoPercentage ──────────────────────────────────────────

    /// <summary>
    /// Query 1: porcentaje mayor a 100 (doble conversión Tasa×100).
    /// Query 2: porcentaje negativo.
    /// </summary>
    private async Task<IReadOnlyList<SchemaAuditEntry>> ValidateImpuestoPercentageAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // Query 1: > 100%
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM catalogos.TipoImpuesto
            WHERE Porcentaje > 100 AND Borrado = 0";
        var overHundred = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (overHundred > 0)
            results.Add(new SchemaAuditEntry(AuditSeverity.Critical, "Impuestos",
                $"{overHundred} TipoImpuesto(s) con Porcentaje > 100 (probable doble conversión Tasa×100 al migrar).",
                "UPDATE catalogos.TipoImpuesto SET Porcentaje = Porcentaje / 100 WHERE Porcentaje > 100 AND Borrado = 0"));

        // Query 2: negativo
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM catalogos.TipoImpuesto
            WHERE Porcentaje < 0 AND Borrado = 0";
        var negative = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (negative > 0)
            results.Add(new SchemaAuditEntry(AuditSeverity.Error, "Impuestos",
                $"{negative} TipoImpuesto(s) con Porcentaje negativo.",
                "Revisar y corregir manualmente los porcentajes negativos."));

        if (overHundred == 0 && negative == 0)
            results.Add(new SchemaAuditEntry(AuditSeverity.Info, "Impuestos",
                "TipoImpuesto: todos los porcentajes en rango válido (0–100)."));

        return results;
    }

    // ── ValidateAbreviaturaTruncation ──────────────────────────────────────

    /// <summary>
    /// Detecta UnidadesMedida cuya Abreviatura tiene exactamente 20 caracteres,
    /// indicio de truncamiento al migrar desde dbo (límite aplicado en la migración).
    /// </summary>
    private async Task<IReadOnlyList<SchemaAuditEntry>> ValidateAbreviaturaTruncationAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM catalogos.UnidadMedida
            WHERE LEN(Abreviatura) = 20 AND Borrado = 0";
        var truncated = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(truncated > 0
            ? new SchemaAuditEntry(AuditSeverity.Warning, "UnidadMedida",
                $"{truncated} UnidadMedida(s) con Abreviatura de exactamente 20 chars (posible truncamiento en migración).",
                "Verificar manualmente: SELECT Nombre, Abreviatura FROM catalogos.UnidadMedida WHERE LEN(Abreviatura) = 20")
            : new SchemaAuditEntry(AuditSeverity.Info, "UnidadMedida",
                "UnidadMedida: sin indicios de truncamiento en Abreviatura."));

        return results;
    }

    // ── ValidateGeography ───────────────────────────────────────────────────

    /// <summary>
    /// Verifica que cada Estado tenga un PaisId que exista en catalogos.Pais.
    /// </summary>
    private async Task<IReadOnlyList<SchemaAuditEntry>> ValidateGeographyAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM catalogos.Estado e
            WHERE e.Borrado = 0
              AND NOT EXISTS (
                  SELECT 1 FROM catalogos.Pais p
                  WHERE p.Id = e.PaisId AND p.Borrado = 0
              )";
        var estadosSinPais = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(estadosSinPais > 0
            ? new SchemaAuditEntry(AuditSeverity.Error, "Geografía",
                $"{estadosSinPais} Estado(s) con PaisId inválido o apuntando a un País borrado.",
                "Actualizar PaisId en catalogos.Estado. La migración asignó México por defecto a todos.")
            : new SchemaAuditEntry(AuditSeverity.Info, "Geografía",
                "Geografía: todas las relaciones Estado → País son válidas."));

        return results;
    }

    // ── ValidateNullRequiredFields ──────────────────────────────────────────

    /// <summary>
    /// Una query por tabla para detectar campos obligatorios en NULL.
    /// </summary>
    private async Task<IReadOnlyList<SchemaAuditEntry>> ValidateNullRequiredFieldsAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // (Tabla, Query de conteo de NULLs críticos)
        var checks = new (string Label, string Sql)[]
        {
            ("catalogos.TipoProducto",
             "SELECT COUNT(*) FROM catalogos.TipoProducto WHERE Nombre IS NULL OR EmpresaId IS NULL OR UsuarioCreacionId IS NULL"),
            ("catalogos.UnidadMedida",
             "SELECT COUNT(*) FROM catalogos.UnidadMedida WHERE Nombre IS NULL OR EmpresaId IS NULL OR UsuarioCreacionId IS NULL"),
            ("catalogos.TipoImpuesto",
             "SELECT COUNT(*) FROM catalogos.TipoImpuesto WHERE Nombre IS NULL OR EmpresaId IS NULL OR UsuarioCreacionId IS NULL"),
            ("catalogos.CategoriaProducto",
             "SELECT COUNT(*) FROM catalogos.CategoriaProducto WHERE Nombre IS NULL OR EmpresaId IS NULL OR UsuarioCreacionId IS NULL"),
            ("catalogos.Moneda",
             "SELECT COUNT(*) FROM catalogos.Moneda WHERE Clave IS NULL OR Nombre IS NULL"),
            ("catalogos.FormaPago",
             "SELECT COUNT(*) FROM catalogos.FormaPago WHERE Clave IS NULL OR Nombre IS NULL"),
            ("catalogos.Pais",
             "SELECT COUNT(*) FROM catalogos.Pais WHERE Nombre IS NULL"),
        };

        var totalNull = 0;
        foreach (var (label, sql) in checks)
        {
            cmd.CommandText = sql;
            var count = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
            if (count > 0)
            {
                totalNull += count;
                results.Add(new SchemaAuditEntry(AuditSeverity.Error, "Campos NULL",
                    $"{label}: {count} registro(s) con campos obligatorios en NULL.",
                    $"Actualizar los registros con NULL en {label} antes de operar el sistema."));
            }
        }

        if (totalNull == 0)
            results.Add(new SchemaAuditEntry(AuditSeverity.Info, "Campos NULL",
                "Todos los catálogos tienen sus campos obligatorios poblados."));

        return results;
    }

    // ── ValidateCatalogCounts ───────────────────────────────────────────────

    /// <summary>
    /// Una query por catálogo. Warning si está vacío, Info con el total si tiene datos.
    /// </summary>
    private async Task<IReadOnlyList<SchemaAuditEntry>> ValidateCatalogCountsAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        var catalogs = new (string Nombre, string Sql)[]
        {
            ("Pais",              "SELECT COUNT(*) FROM catalogos.Pais              WHERE Borrado = 0"),
            ("Estado",            "SELECT COUNT(*) FROM catalogos.Estado            WHERE Borrado = 0"),
            ("Moneda",            "SELECT COUNT(*) FROM catalogos.Moneda            WHERE Borrado = 0"),
            ("FormaPago",         "SELECT COUNT(*) FROM catalogos.FormaPago         WHERE Borrado = 0"),
            ("TipoProducto",      "SELECT COUNT(*) FROM catalogos.TipoProducto      WHERE Borrado = 0"),
            ("UnidadMedida",      "SELECT COUNT(*) FROM catalogos.UnidadMedida      WHERE Borrado = 0"),
            ("TipoImpuesto",      "SELECT COUNT(*) FROM catalogos.TipoImpuesto      WHERE Borrado = 0"),
            ("CategoriaProducto", "SELECT COUNT(*) FROM catalogos.CategoriaProducto WHERE Borrado = 0"),
            ("MetodoPago",        "SELECT COUNT(*) FROM catalogos.MetodoPago        WHERE Borrado = 0"),
            ("Producto",          "SELECT COUNT(*) FROM core.Producto               WHERE Borrado = 0"),
        };

        foreach (var (nombre, sql) in catalogs)
        {
            cmd.CommandText = sql;
            var count = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
            results.Add(count == 0
                ? new SchemaAuditEntry(AuditSeverity.Warning, "Conteos",
                    $"catalogos.{nombre}: sin registros activos.",
                    $"Cargar datos en catalogos.{nombre} antes de usar el módulo correspondiente.")
                : new SchemaAuditEntry(AuditSeverity.Info, "Conteos",
                    $"catalogos.{nombre}: {count} registro(s) activo(s)."));
        }

        return results;
    }
}

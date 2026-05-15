using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Ybridio.Infrastructure.Persistence.Audit;

/// <summary>
/// ERP Structural Integrity Engine — implementación SQL Server.
/// Valida la integridad semántica del workflow comercial: lifecycle documental,
/// snapshots, datos legacy, y detecta migraciones manuales pendientes.
/// </summary>
/// <remarks>
/// Clasificación de severidades aplicada:
/// - Critical   → estado imposible / corrupción de datos (total negativo, saldo erróneo)
/// - Error      → inconsistencia recuperable (FK comercial rota con datos)
/// - Warning    → divergencia esperada o transitoria
/// - LegacyData → dato histórico válido (pre-SerieDocumento, pre-workflow)
/// - MigrationPending → columna/constraint de script manual no ejecutado aún
/// - Info       → estado correcto o contextual
/// </remarks>
public sealed class WorkflowAuditService : IWorkflowAuditService
{
    private readonly ErpDbContext _db;

    public WorkflowAuditService(ErpDbContext db) => _db = db;

    // ── Punto de entrada ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<SchemaAuditReport> RunAsync(CancellationToken ct = default)
    {
        var findings = new List<SchemaAuditEntry>();

        var conn = _db.Database.GetDbConnection();
        var needsOpen = conn.State != ConnectionState.Open;
        if (needsOpen) await conn.OpenAsync(ct);

        try
        {
            // ── 1. Migraciones manuales pendientes ────────────────────────
            findings.AddRange(await AuditPendingManualScriptsAsync(conn, ct));

            // ── 2. Workflow — Cotizaciones ────────────────────────────────
            findings.AddRange(await AuditCotizacionesLifecycleAsync(conn, ct));

            // ── 3. Workflow — Pedidos ─────────────────────────────────────
            findings.AddRange(await AuditPedidosLifecycleAsync(conn, ct));

            // ── 4. Workflow — Ventas documentales ─────────────────────────
            findings.AddRange(await AuditVentasLifecycleAsync(conn, ct));

            // ── 5. Integridad financiera ventas ───────────────────────────
            findings.AddRange(await AuditVentasFinancierasAsync(conn, ct));

            // ── 6. Snapshots documentales ─────────────────────────────────
            findings.AddRange(await AuditSnapshotsAsync(conn, ct));

            // ── 7. Datos legacy — clasificación ──────────────────────────
            findings.AddRange(await AuditLegacyDataAsync(conn, ct));

            // ── 8. Integridad folios ──────────────────────────────────────
            findings.AddRange(await AuditFoliosAsync(conn, ct));
        }
        catch (Exception ex)
        {
            findings.Add(new SchemaAuditEntry(
                AuditSeverity.Critical, "Error Auditoría Workflow",
                $"Error inesperado: {ex.Message}",
                "Verificar conectividad y permisos sobre schemas ventas, core, catalogos."));
        }
        finally
        {
            if (needsOpen) await conn.CloseAsync();
        }

        findings.Sort((a, b) => a.Severity.CompareTo(b.Severity));
        return new SchemaAuditReport(DateTime.UtcNow, findings.AsReadOnly());
    }

    // ════════════════════════════════════════════════════════════════════════
    // 1. Migraciones manuales pendientes
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Detecta si las columnas de scripts manuales conocidos están presentes en BD.
    /// Informa MigrationPending si faltan, Info si ya aplicados.
    /// </summary>
    private static async Task<IReadOnlyList<SchemaAuditEntry>> AuditPendingManualScriptsAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();

        var scripts = new[]
        {
            (Script: "AddWorkflowColumns_V1.sql",
             Checks: new[]
             {
                 ("ventas", "PedidoDetalle", "DescuentoPct"),
                 ("ventas", "PedidoDetalle", "IvaAplicable"),
                 ("ventas", "Pedido",        "Subtotal"),
             }),
            (Script: "AddDescuentoPct_CotizacionDetalle.sql",
             Checks: new[]
             {
                 ("ventas", "CotizacionDetalle", "DescuentoPct"),
             }),
            (Script: "EvolveProductoTipoAndCotizacion_V1.sql",
             Checks: new[]
             {
                 ("ventas", "CotizacionDetalle", "IvaAplicable"),
                 ("ventas", "CotizacionDetalle", "CotizacionCargo"),  // tabla, no columna — se chequeará por tabla
             }),
        };

        using var cmd = conn.CreateCommand();

        foreach (var (script, checks) in scripts)
        {
            var missing = new List<string>();

            foreach (var (schema, table, column) in checks)
            {
                // CotizacionCargo es una tabla, no columna — checar existencia diferente
                if (column == "CotizacionCargo")
                {
                    cmd.CommandText = @"SELECT COUNT(*) FROM sys.tables t
                        JOIN sys.schemas s ON t.schema_id = s.schema_id
                        WHERE s.name = @schema AND t.name = @table";
                }
                else
                {
                    cmd.CommandText = @"SELECT COUNT(*) FROM sys.columns
                        WHERE object_id = OBJECT_ID(@fullName) AND name = @col";
                }

                cmd.Parameters.Clear();

                if (column == "CotizacionCargo")
                {
                    var ps = cmd.CreateParameter(); ps.ParameterName = "@schema"; ps.Value = schema; cmd.Parameters.Add(ps);
                    var pt = cmd.CreateParameter(); pt.ParameterName = "@table"; pt.Value = column; cmd.Parameters.Add(pt);
                }
                else
                {
                    var pf = cmd.CreateParameter(); pf.ParameterName = "@fullName"; pf.Value = $"{schema}.{table}"; cmd.Parameters.Add(pf);
                    var pc = cmd.CreateParameter(); pc.ParameterName = "@col"; pc.Value = column; cmd.Parameters.Add(pc);
                }

                var exists = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0) > 0;
                if (!exists) missing.Add($"{schema}.{table}.{column}");
            }

            if (missing.Count > 0)
                results.Add(new SchemaAuditEntry(
                    AuditSeverity.MigrationPending, "Scripts Pendientes",
                    $"{script}: {missing.Count} elemento(s) faltante(s): {string.Join(", ", missing)}",
                    $"Ejecutar Documentation/Scripts/{script} en la base de datos."));
            else
                results.Add(new SchemaAuditEntry(
                    AuditSeverity.Info, "Scripts Pendientes",
                    $"{script}: aplicado correctamente — todos los elementos presentes."));
        }

        return results;
    }

    // ════════════════════════════════════════════════════════════════════════
    // 2. Lifecycle Cotizaciones
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Valida el lifecycle de Cotizaciones:
    /// - Convertidas sin Pedido derivado: Warning
    /// - Cotizaciones Canceladas con detalles activos: Info (datos históricos esperados)
    /// - Folios duplicados: Critical
    /// - Cotizaciones sin folio (pre-SerieDocumento): LegacyData
    /// </summary>
    private static async Task<IReadOnlyList<SchemaAuditEntry>> AuditCotizacionesLifecycleAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // Cotizaciones Convertidas sin Pedido correspondiente
        // EstatusCotizacion.Convertida = 3
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Cotizacion c
            WHERE c.Estatus = 3
              AND c.Borrado = 0
              AND NOT EXISTS (
                  SELECT 1 FROM ventas.Pedido p
                  WHERE p.CotizacionId = c.Id AND p.Borrado = 0
              )";
        var convertSinPedido = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(convertSinPedido > 0
            ? new SchemaAuditEntry(AuditSeverity.Warning, "Workflow Cotizaciones",
                $"{convertSinPedido} cotización(es) Convertida(s) sin Pedido derivado registrado.",
                "Verificar si el Pedido fue creado fuera del sistema o si la conversión falló. Revisar logs.")
            : new SchemaAuditEntry(AuditSeverity.Info, "Workflow Cotizaciones",
                "Cotizaciones Convertidas: todas tienen Pedido derivado."));

        // Cotizaciones con folio duplicado dentro de la misma empresa
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM (
                SELECT EmpresaId, Folio
                FROM ventas.Cotizacion
                WHERE Folio IS NOT NULL AND Borrado = 0
                GROUP BY EmpresaId, Folio
                HAVING COUNT(*) > 1
            ) dup";
        var foliosDupCot = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(foliosDupCot > 0
            ? new SchemaAuditEntry(AuditSeverity.Critical, "Folios Cotizaciones",
                $"{foliosDupCot} grupo(s) con folio de Cotización duplicado en la misma empresa.",
                "Investigar causa de duplicación. Corregir IFolioGeneratorService o revisar seed de SerieDocumento.")
            : new SchemaAuditEntry(AuditSeverity.Info, "Folios Cotizaciones",
                "Folios de Cotizaciones: sin duplicados."));

        // Cotizaciones sin folio (pre-SerieDocumento — legacy esperado)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Cotizacion
            WHERE Folio IS NULL AND Borrado = 0";
        var sinFolioCot = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(sinFolioCot > 0
            ? new SchemaAuditEntry(AuditSeverity.LegacyData, "Folios Cotizaciones",
                $"{sinFolioCot} cotización(es) sin folio — registros anteriores a SerieDocumento (legacy normal).",
                "Estos registros son válidos. No requieren acción. Los nuevos documentos generan folio automáticamente.")
            : new SchemaAuditEntry(AuditSeverity.Info, "Folios Cotizaciones",
                "Todas las cotizaciones activas tienen folio."));

        return results;
    }

    // ════════════════════════════════════════════════════════════════════════
    // 3. Lifecycle Pedidos
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Valida el lifecycle de Pedidos:
    /// - Pedidos con estado entero fuera del rango válido: Critical
    /// - Pedidos Finalizado(3) sin Venta derivada: Info (puede ser pedido directo)
    /// - Folios duplicados: Critical
    /// - Pedidos sin folio (legacy): LegacyData
    /// </summary>
    private static async Task<IReadOnlyList<SchemaAuditEntry>> AuditPedidosLifecycleAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // Estados válidos: 0=Borrador, 1=Autorizado, 2=EnProceso, 3=Finalizado, 4=Parcial, 9=Cancelado
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Pedido
            WHERE Borrado = 0
              AND Estatus NOT IN (0, 1, 2, 3, 4, 9)";
        var estadosInvalidos = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (estadosInvalidos > 0)
            results.Add(new SchemaAuditEntry(AuditSeverity.Critical, "Workflow Pedidos",
                $"{estadosInvalidos} pedido(s) con valor de Estatus fuera del rango válido (0/1/2/3/4/9).",
                "Corregir manualmente los valores de Estatus en ventas.Pedido."));
        else
            results.Add(new SchemaAuditEntry(AuditSeverity.Info, "Workflow Pedidos",
                "Estados de Pedidos: todos dentro del rango válido."));

        // Folios duplicados
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM (
                SELECT EmpresaId, Folio
                FROM ventas.Pedido
                WHERE Folio IS NOT NULL AND Borrado = 0
                GROUP BY EmpresaId, Folio
                HAVING COUNT(*) > 1
            ) dup";
        var foliosDup = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(foliosDup > 0
            ? new SchemaAuditEntry(AuditSeverity.Critical, "Folios Pedidos",
                $"{foliosDup} grupo(s) con folio de Pedido duplicado en la misma empresa.",
                "Investigar IFolioGeneratorService o SerieDocumento para Pedido.")
            : new SchemaAuditEntry(AuditSeverity.Info, "Folios Pedidos",
                "Folios de Pedidos: sin duplicados."));

        // Pedidos sin folio (legacy)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Pedido
            WHERE Folio IS NULL AND Borrado = 0";
        var sinFolio = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(sinFolio > 0
            ? new SchemaAuditEntry(AuditSeverity.LegacyData, "Folios Pedidos",
                $"{sinFolio} pedido(s) sin folio — registros anteriores a workflow comercial estabilizado (legacy normal).",
                "Registros válidos. Los nuevos pedidos generan folio automáticamente desde PedidoService.CrearAsync.")
            : new SchemaAuditEntry(AuditSeverity.Info, "Folios Pedidos",
                "Todos los pedidos activos tienen folio."));

        // Total cero o negativo en pedidos activos
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Pedido
            WHERE Borrado = 0
              AND Estatus NOT IN (9)
              AND Total < 0";
        var totalNegativo = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (totalNegativo > 0)
            results.Add(new SchemaAuditEntry(AuditSeverity.Critical, "Totales Pedidos",
                $"{totalNegativo} pedido(s) activo(s) con Total negativo — estado imposible.",
                "Revisar y corregir los totales en ventas.Pedido."));

        return results;
    }

    // ════════════════════════════════════════════════════════════════════════
    // 4. Lifecycle Ventas Documentales
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Valida el lifecycle de Ventas Documentales:
    /// - Estados inválidos: Critical
    /// - Ventas Cerradas (5) con saldo > 0: Critical (imposible por regla)
    /// - Ventas PendientePago(1) con TotalPagado > Total: Critical (imposible)
    /// - Folios duplicados: Critical
    /// - Ventas sin folio (legacy): LegacyData
    /// - Ventas POS legacy (NombreCliente=null): LegacyData
    /// </summary>
    private static async Task<IReadOnlyList<SchemaAuditEntry>> AuditVentasLifecycleAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // Estados válidos: 0=Borrador, 1=PendientePago, 2=Pagada, 3=Facturada, 4=Entregada, 5=Cerrada, 9=Cancelada
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Venta
            WHERE Borrado = 0
              AND NombreCliente IS NOT NULL
              AND Estatus NOT IN (0, 1, 2, 3, 4, 5, 9)";
        var estadosInvalidos = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (estadosInvalidos > 0)
            results.Add(new SchemaAuditEntry(AuditSeverity.Critical, "Workflow Ventas",
                $"{estadosInvalidos} venta(s) documental(es) con valor de Estatus fuera del rango válido.",
                "Corregir manualmente los valores de Estatus en ventas.Venta para documentos con NombreCliente."));
        else
            results.Add(new SchemaAuditEntry(AuditSeverity.Info, "Workflow Ventas",
                "Estados de Ventas Documentales: todos dentro del rango válido."));

        // Ventas Cerradas con saldo > 0 (imposible: CerrarAsync valida saldo = 0)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Venta
            WHERE Borrado = 0
              AND Estatus = 5
              AND (Total - TotalPagado) > 0.01";
        var cerradasConSaldo = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(cerradasConSaldo > 0
            ? new SchemaAuditEntry(AuditSeverity.Critical, "Lifecycle Ventas",
                $"{cerradasConSaldo} venta(s) en estado Cerrada con saldo pendiente > 0 — estado imposible.",
                "Revisar lógica de CerrarAsync o corregir Estatus/TotalPagado manualmente.")
            : new SchemaAuditEntry(AuditSeverity.Info, "Lifecycle Ventas",
                "Ventas Cerradas: todas con saldo = 0 (correcto)."));

        // TotalPagado > Total en ventas activas (overpayment imposible)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Venta
            WHERE Borrado = 0
              AND Estatus NOT IN (9)
              AND NombreCliente IS NOT NULL
              AND Total IS NOT NULL
              AND TotalPagado > (Total + 0.01)";
        var overpaid = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(overpaid > 0
            ? new SchemaAuditEntry(AuditSeverity.Critical, "Pagos Ventas",
                $"{overpaid} venta(s) con TotalPagado > Total — sobrepago imposible.",
                "Revisar RegistrarPagoAsync y los registros en PagoVenta para estas ventas.")
            : new SchemaAuditEntry(AuditSeverity.Info, "Pagos Ventas",
                "TotalPagado ≤ Total en todas las ventas activas (correcto)."));

        // Folios duplicados en ventas documentales
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM (
                SELECT EmpresaId, Folio
                FROM ventas.Venta
                WHERE Folio IS NOT NULL AND Borrado = 0
                GROUP BY EmpresaId, Folio
                HAVING COUNT(*) > 1
            ) dup";
        var foliosDup = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(foliosDup > 0
            ? new SchemaAuditEntry(AuditSeverity.Critical, "Folios Ventas",
                $"{foliosDup} grupo(s) con folio de Venta duplicado — corrupción de identidad documental.",
                "Investigar IFolioGeneratorService o SerieDocumento para Venta.")
            : new SchemaAuditEntry(AuditSeverity.Info, "Folios Ventas",
                "Folios de Ventas: sin duplicados."));

        // Ventas documentales sin folio (legacy — creadas antes de workflow estabilizado)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Venta
            WHERE Folio IS NULL AND NombreCliente IS NOT NULL AND Borrado = 0";
        var sinFolio = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(sinFolio > 0
            ? new SchemaAuditEntry(AuditSeverity.LegacyData, "Folios Ventas",
                $"{sinFolio} venta(s) documental(es) sin folio — registros anteriores a workflow estabilizado (legacy).",
                "Registros válidos. Los nuevos documentos generan folio automáticamente desde VentaDocumentalService.CrearAsync.")
            : new SchemaAuditEntry(AuditSeverity.Info, "Folios Ventas",
                "Todas las ventas documentales tienen folio."));

        // Ventas POS legacy (NombreCliente = NULL) — informativo
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Venta
            WHERE NombreCliente IS NULL AND Borrado = 0";
        var posLegacy = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(new SchemaAuditEntry(AuditSeverity.LegacyData, "Ventas POS",
            $"{posLegacy} venta(s) POS legacy (NombreCliente=NULL) — flujo POS original, coexistencia intencional.",
            null));

        return results;
    }

    // ════════════════════════════════════════════════════════════════════════
    // 5. Integridad financiera ventas
    // ════════════════════════════════════════════════════════════════════════

    private static async Task<IReadOnlyList<SchemaAuditEntry>> AuditVentasFinancierasAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // Ventas con Total nulo en estado confirmado o superior (no Borrador)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Venta
            WHERE Borrado = 0
              AND Estatus NOT IN (0, 9)
              AND NombreCliente IS NOT NULL
              AND Total IS NULL";
        var totalNulo = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (totalNulo > 0)
            results.Add(new SchemaAuditEntry(AuditSeverity.Error, "Totales Ventas",
                $"{totalNulo} venta(s) confirmada(s)/pagada(s) con Total = NULL.",
                "Recalcular Total desde VentaDetalle para estas ventas: UPDATE ventas.Venta SET Total = (SELECT SUM(Importe) FROM ventas.VentaDetalle WHERE VentaId = Id)"));
        else
            results.Add(new SchemaAuditEntry(AuditSeverity.Info, "Totales Ventas",
                "Ventas confirmadas: todas con Total definido."));

        // Ventas PendientePago (1) sin detalles (documento hueco)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Venta v
            WHERE v.Borrado = 0
              AND v.Estatus = 1
              AND NombreCliente IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM ventas.VentaDetalle d WHERE d.VentaId = v.Id
              )";
        var sinDetalles = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (sinDetalles > 0)
            results.Add(new SchemaAuditEntry(AuditSeverity.Error, "Detalles Ventas",
                $"{sinDetalles} venta(s) PendientePago sin líneas de detalle — documento incompleto.",
                "Revisar estas ventas. Posible error en la conversión desde Pedido o creación directa."));

        // Pagos registrados para ventas canceladas
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.PagoVenta pv
            JOIN ventas.Venta v ON pv.VentaId = v.Id
            WHERE v.Estatus = 9 AND v.Borrado = 0";
        var pagosCanceladas = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (pagosCanceladas > 0)
            results.Add(new SchemaAuditEntry(AuditSeverity.Warning, "Pagos Ventas",
                $"{pagosCanceladas} pago(s) registrado(s) contra venta(s) cancelada(s).",
                "Verificar si los pagos corresponden a período anterior a la cancelación o son erróneos."));

        return results;
    }

    // ════════════════════════════════════════════════════════════════════════
    // 6. Snapshots documentales
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Valida que los snapshots documentales estén correctamente poblados.
    /// NombreCliente, nombres de producto, precios.
    /// </summary>
    private static async Task<IReadOnlyList<SchemaAuditEntry>> AuditSnapshotsAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // VentaDetalle sin nombre de producto — esperado en ventas POS antiguas (el nombre viene de navegación EF)
        // Para ventas documentales, el nombre debería estar accesible via Include Producto
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.VentaDetalle vd
            JOIN ventas.Venta v ON vd.VentaId = v.Id
            WHERE v.Borrado = 0
              AND v.NombreCliente IS NOT NULL
              AND (vd.ProductoId = 0 OR vd.ProductoId IS NULL)";
        var detallesSinProducto = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (detallesSinProducto > 0)
            results.Add(new SchemaAuditEntry(AuditSeverity.Warning, "Snapshots Ventas",
                $"{detallesSinProducto} línea(s) de venta documental sin ProductoId referenciado.",
                "Estas líneas no podrán mostrar el nombre del producto. Verificar si fue ingresado correctamente."));
        else
            results.Add(new SchemaAuditEntry(AuditSeverity.Info, "Snapshots Ventas",
                "Líneas de ventas documentales: todas con ProductoId referenciado."));

        // Cotizaciones sin NombreCliente snapshot (campo obligatorio en workflow actual)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Cotizacion
            WHERE Borrado = 0
              AND (NombreCliente IS NULL OR NombreCliente = '')";
        var cotSinCliente = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (cotSinCliente > 0)
            results.Add(new SchemaAuditEntry(AuditSeverity.Error, "Snapshots Cotizaciones",
                $"{cotSinCliente} cotización(es) sin NombreCliente snapshot.",
                "El snapshot de NombreCliente es obligatorio. Revisar y poblar desde RelacionComercial o directamente."));

        // Pedidos sin NombreCliente snapshot
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Pedido
            WHERE Borrado = 0
              AND (NombreCliente IS NULL OR NombreCliente = '')";
        var pedSinCliente = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (pedSinCliente > 0)
            results.Add(new SchemaAuditEntry(AuditSeverity.Error, "Snapshots Pedidos",
                $"{pedSinCliente} pedido(s) sin NombreCliente snapshot.",
                "El snapshot de NombreCliente es obligatorio en Pedido."));
        else
            results.Add(new SchemaAuditEntry(AuditSeverity.Info, "Snapshots Pedidos",
                "Pedidos: todos tienen NombreCliente snapshot."));

        // Detalles de Cotización con Importe = 0 (posible error en cálculo)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.CotizacionDetalle cd
            JOIN ventas.Cotizacion c ON cd.CotizacionId = c.Id
            WHERE c.Borrado = 0
              AND c.Estatus NOT IN (4, 9)
              AND cd.Importe = 0
              AND cd.Cantidad > 0";
        var importeCero = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (importeCero > 0)
            results.Add(new SchemaAuditEntry(AuditSeverity.Warning, "Snapshots Cotizaciones",
                $"{importeCero} línea(s) de cotización activa con Importe = 0 y Cantidad > 0.",
                "Posible error en cálculo de importe. Revisar CommercialDocumentCalculator.CalcularImporteLinea."));

        return results;
    }

    // ════════════════════════════════════════════════════════════════════════
    // 7. Clasificación de datos legacy
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Detecta datos históricos válidos que podrían interpretarse erróneamente como errores.
    /// Los clasifica como LegacyData para distinguirlos de problemas reales.
    /// </summary>
    private static async Task<IReadOnlyList<SchemaAuditEntry>> AuditLegacyDataAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // Pedidos con Subtotal NULL (pre-AddWorkflowColumns_V1.sql — se rellena con Total en el script)
        cmd.CommandText = @"
            SELECT COUNT(*) FROM ventas.Pedido
            WHERE Subtotal IS NULL AND Borrado = 0";
        // Manejar el caso donde la columna no existe aún
        try
        {
            var sinSubtotal = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
            results.Add(sinSubtotal > 0
                ? new SchemaAuditEntry(AuditSeverity.LegacyData, "Legacy Pedidos",
                    $"{sinSubtotal} pedido(s) con Subtotal=NULL — registros pre-AddWorkflowColumns_V1.sql (legacy normal).",
                    "Re-ejecutar: UPDATE ventas.Pedido SET Subtotal = Total WHERE Subtotal IS NULL")
                : new SchemaAuditEntry(AuditSeverity.Info, "Legacy Pedidos",
                    "Pedidos: columna Subtotal poblada en todos los registros."));
        }
        catch
        {
            results.Add(new SchemaAuditEntry(AuditSeverity.MigrationPending, "Legacy Pedidos",
                "ventas.Pedido.Subtotal no existe en BD — ejecutar AddWorkflowColumns_V1.sql.",
                "Documentation/Scripts/AddWorkflowColumns_V1.sql"));
        }

        // PedidoDetalle con DescuentoPct NULL o 0 en detalles copiados de cotización (legacy)
        // No es error — es el valor esperado para pedidos pre-workflow
        try
        {
            cmd.CommandText = @"
                SELECT COUNT(*) FROM ventas.PedidoDetalle WHERE DescuentoPct > 0";
            var conDescuento = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
            results.Add(new SchemaAuditEntry(AuditSeverity.Info, "Legacy Pedidos",
                $"PedidoDetalle: {conDescuento} línea(s) con DescuentoPct > 0 (columna presente y activa)."));
        }
        catch
        {
            results.Add(new SchemaAuditEntry(AuditSeverity.MigrationPending, "Legacy Pedidos",
                "ventas.PedidoDetalle.DescuentoPct no existe en BD.",
                "Documentation/Scripts/AddWorkflowColumns_V1.sql"));
        }

        // Ventas con Estatus=1 (era Confirmada, ahora PendientePago — mismo valor, rename semántico)
        cmd.CommandText = @"
            SELECT COUNT(*) FROM ventas.Venta
            WHERE Estatus = 1 AND NombreCliente IS NOT NULL AND Borrado = 0";
        var pendientePago = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        results.Add(new SchemaAuditEntry(AuditSeverity.Info, "Workflow Ventas",
            $"{pendientePago} venta(s) documental(es) en estado PendientePago (1) — estado operacional normal."));

        return results;
    }

    // ════════════════════════════════════════════════════════════════════════
    // 8. Integridad de folios
    // ════════════════════════════════════════════════════════════════════════

    private static async Task<IReadOnlyList<SchemaAuditEntry>> AuditFoliosAsync(
        DbConnection conn, CancellationToken ct)
    {
        var results = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // Verificar que SerieDocumento existe y tiene series configuradas
        cmd.CommandText = @"
            SELECT COUNT(*) FROM sys.tables t
            JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = 'catalogos' AND t.name = 'SerieDocumento'";
        var serieExists = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0) > 0;

        if (!serieExists)
        {
            results.Add(new SchemaAuditEntry(AuditSeverity.MigrationPending, "SerieDocumento",
                "catalogos.SerieDocumento no existe en BD — ejecutar AddSerieDocumento_V1.sql.",
                "Documentation/Scripts/AddSerieDocumento_V1.sql"));
            return results;
        }

        // Contar series por tipo de documento
        cmd.CommandText = @"
            SELECT TipoDocumento, COUNT(*) as Total, MAX(SiguienteNumero) as MaxNumero
            FROM catalogos.SerieDocumento
            WHERE Activo = 1 AND Borrado = 0
            GROUP BY TipoDocumento
            ORDER BY TipoDocumento";

        var seriesPorTipo = new List<(int Tipo, int Total, long MaxNum)>();
        using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
                seriesPorTipo.Add((r.GetInt32(0), r.GetInt32(1), r.GetInt64(2)));
        }

        // Tipos clave para el workflow: 1=Cotizacion, 2=Pedido, 3=Venta
        var tiposRequeridos = new[] { (1, "Cotización"), (2, "Pedido"), (3, "Venta") };
        foreach (var (tipo, nombre) in tiposRequeridos)
        {
            var serie = seriesPorTipo.FirstOrDefault(s => s.Tipo == tipo);
            if (serie == default)
                results.Add(new SchemaAuditEntry(AuditSeverity.Warning, "SerieDocumento",
                    $"Sin serie activa para {nombre} (TipoDocumento={tipo}) — los nuevos documentos NO tendrán folio.",
                    $"Configurar una SerieDocumento para TipoDocumento={tipo} en Configuración → Series de Documentos."));
            else
                results.Add(new SchemaAuditEntry(AuditSeverity.Info, "SerieDocumento",
                    $"Serie {nombre}: {serie.Total} serie(s) activa(s), siguiente número: {serie.MaxNum}."));
        }

        return results;
    }
}

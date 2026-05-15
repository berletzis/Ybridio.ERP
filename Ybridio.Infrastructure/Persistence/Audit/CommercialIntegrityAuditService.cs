using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Ybridio.Infrastructure.Persistence.Audit;

/// <summary>
/// Auditoría de integridad comercial — implementación SQL Server.
/// Valida la coherencia del workflow Cotización → Pedido → Venta → Pago → Cierre
/// con foco en consistencia financiera y trazabilidad entre documentos.
/// </summary>
/// <remarks>
/// Módulos: Cotizaciones, Pedidos, Ventas, Pagos, CxC.
/// Todos los findings usan la propiedad <see cref="SchemaAuditEntry.Module"/> para grouping.
/// </remarks>
public sealed class CommercialIntegrityAuditService : ICommercialIntegrityAuditService
{
    private readonly ErpDbContext _db;

    // Módulos para la propiedad Module de SchemaAuditEntry
    private const string ModCot = "Cotizaciones";
    private const string ModPed = "Pedidos";
    private const string ModVta = "Ventas";
    private const string ModPag = "Pagos";
    private const string ModCxC = "CxC";
    private const string ModGen = "General";

    public CommercialIntegrityAuditService(ErpDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<SchemaAuditReport> RunAsync(CancellationToken ct = default)
    {
        var findings = new List<SchemaAuditEntry>();

        var conn = _db.Database.GetDbConnection();
        var needsOpen = conn.State != ConnectionState.Open;
        if (needsOpen) await conn.OpenAsync(ct);

        try
        {
            // A. Cadena de conversión COT→PED→VTA
            findings.AddRange(await AuditConversionChainAsync(conn, ct));
            ct.ThrowIfCancellationRequested();

            // B. Consistencia de totales (detalles vs encabezado)
            findings.AddRange(await AuditFinancialTotalsAsync(conn, ct));
            ct.ThrowIfCancellationRequested();

            // C. Integridad de pagos
            findings.AddRange(await AuditPaymentIntegrityAsync(conn, ct));
            ct.ThrowIfCancellationRequested();

            // D. Documentos estancados (aging operacional)
            findings.AddRange(await AuditDocumentAgingAsync(conn, ct));
            ct.ThrowIfCancellationRequested();

            // E. Referencias cruzadas de productos
            findings.AddRange(await AuditProductReferencesAsync(conn, ct));
            ct.ThrowIfCancellationRequested();

            // F. Crédito y CxC — coherencia
            findings.AddRange(await AuditCreditAndCxCAsync(conn, ct));
            ct.ThrowIfCancellationRequested();

            // G. Audit trail readiness
            findings.AddRange(await AuditTrailReadinessAsync(conn, ct));
        }
        catch (OperationCanceledException)
        {
            findings.Add(new SchemaAuditEntry(
                AuditSeverity.Info, "Auditoría", "Auditoría cancelada por el usuario.",
                null, ModGen));
        }
        catch (Exception ex)
        {
            findings.Add(new SchemaAuditEntry(
                AuditSeverity.Critical, "Error",
                $"Error inesperado en auditoría comercial: {ex.Message}",
                "Verificar conectividad y permisos.", ModGen));
        }
        finally
        {
            if (needsOpen) await conn.CloseAsync();
        }

        findings.Sort((a, b) => a.Severity.CompareTo(b.Severity));
        return new SchemaAuditReport(DateTime.UtcNow, findings.AsReadOnly());
    }

    // ════════════════════════════════════════════════════════════════════════
    // A. Cadena de conversión COT→PED→VTA
    // ════════════════════════════════════════════════════════════════════════

    private static async Task<IReadOnlyList<SchemaAuditEntry>> AuditConversionChainAsync(
        DbConnection conn, CancellationToken ct)
    {
        var r = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // A1: Pedido referencia Cotización que NO está en estado Convertida (3)
        //     Esperado: Pedido.CotizacionId → Cotizacion.Estatus = 3
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Pedido p
            JOIN ventas.Cotizacion c ON p.CotizacionId = c.Id
            WHERE p.Borrado = 0 AND c.Borrado = 0
              AND c.Estatus != 3";
        var pedConCotNoConvertida = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        r.Add(pedConCotNoConvertida > 0
            ? new(AuditSeverity.Error, "Cadena Conversión",
                $"{pedConCotNoConvertida} pedido(s) referencian una Cotización que NO está en estado Convertida.",
                "La Cotización debería pasar a Convertida al generar el Pedido. Revisar ConvertirAPedidoAsync.",
                ModCot)
            : new(AuditSeverity.Info, "Cadena Conversión",
                "COT→PED: todas las cotizaciones referenciadas están en estado Convertida.", null, ModCot));

        // A2: Cotización Convertida referenciada por más de 1 Pedido activo (duplicación)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM (
                SELECT CotizacionId
                FROM ventas.Pedido
                WHERE CotizacionId IS NOT NULL AND Borrado = 0
                GROUP BY CotizacionId
                HAVING COUNT(*) > 1
            ) dup";
        var cotConMultiPed = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        r.Add(cotConMultiPed > 0
            ? new(AuditSeverity.Warning, "Cadena Conversión",
                $"{cotConMultiPed} cotización(es) originaron más de 1 Pedido activo.",
                "Verificar si la conversión múltiple fue intencional. Una cotización debería originar un único pedido.",
                ModCot)
            : new(AuditSeverity.Info, "Cadena Conversión",
                "COT→PED: sin cotizaciones con múltiples pedidos.", null, ModCot));

        // A3: Venta referencia Pedido borrado o Cancelado
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Venta v
            JOIN ventas.Pedido p ON v.PedidoId = p.Id
            WHERE v.Borrado = 0
              AND (p.Borrado = 1 OR p.Estatus = 9)";
        var vtaConPedInvalido = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        r.Add(vtaConPedInvalido > 0
            ? new(AuditSeverity.Error, "Cadena Conversión",
                $"{vtaConPedInvalido} venta(s) están vinculadas a un Pedido borrado o Cancelado.",
                "Verificar si la venta debe continuar existir o cancelarse también.", ModVta)
            : new(AuditSeverity.Info, "Cadena Conversión",
                "PED→VTA: todas las ventas vinculadas a pedidos válidos.", null, ModVta));

        // A4: Múltiples ventas documentales desde el mismo Pedido (inusual pero puede pasar)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM (
                SELECT PedidoId
                FROM ventas.Venta
                WHERE PedidoId IS NOT NULL AND Borrado = 0
                  AND NombreCliente IS NOT NULL
                GROUP BY PedidoId
                HAVING COUNT(*) > 1
            ) dup";
        var pedConMultiVta = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (pedConMultiVta > 0)
            r.Add(new(AuditSeverity.Warning, "Cadena Conversión",
                $"{pedConMultiVta} pedido(s) con más de 1 venta documental derivada.",
                "Verificar si las ventas múltiples son entregas parciales intencionales.",
                ModPed));

        // A5: Coherencia de total COT vs PED en la conversión (drift > 1%)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Pedido p
            JOIN ventas.Cotizacion c ON p.CotizacionId = c.Id
            WHERE p.Borrado = 0 AND c.Borrado = 0
              AND c.Total > 0
              AND ABS(p.Total - c.Total) / c.Total > 0.01";
        var totalDrift = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        r.Add(totalDrift > 0
            ? new(AuditSeverity.Warning, "Cadena Conversión",
                $"{totalDrift} pedido(s) con Total que difiere > 1% de la Cotización origen.",
                "El Total del Pedido debería preservar el Total de la Cotización. Revisar ConvertirAPedidoAsync.",
                ModPed)
            : new(AuditSeverity.Info, "Cadena Conversión",
                "COT→PED: totales coherentes (drift < 1%).", null, ModPed));

        return r;
    }

    // ════════════════════════════════════════════════════════════════════════
    // B. Consistencia de totales financieros
    // ════════════════════════════════════════════════════════════════════════

    private static async Task<IReadOnlyList<SchemaAuditEntry>> AuditFinancialTotalsAsync(
        DbConnection conn, CancellationToken ct)
    {
        var r = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // B1: Pedido.Total != SUM(PedidoDetalle.Importe) — drift en encabezado
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Pedido p
            WHERE p.Borrado = 0
              AND p.Estatus NOT IN (9)
              AND ABS(p.Total - ISNULL((
                  SELECT SUM(d.Importe)
                  FROM ventas.PedidoDetalle d WHERE d.PedidoId = p.Id
              ), 0)) > 0.01";
        var pedTotalDrift = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        r.Add(pedTotalDrift > 0
            ? new(AuditSeverity.Error, "Totales Financieros",
                $"{pedTotalDrift} pedido(s) activo(s) cuyo Total en encabezado difiere del SUM de detalles.",
                "Recalcular: UPDATE ventas.Pedido SET Total = (SELECT SUM(Importe) FROM ventas.PedidoDetalle WHERE PedidoId=Id) WHERE...",
                ModPed)
            : new(AuditSeverity.Info, "Totales Financieros",
                "Pedidos: Total encabezado = SUM(detalles) en todos los activos.", null, ModPed));

        // B2: Venta.Total != SUM(VentaDetalle.Importe) — drift
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Venta v
            WHERE v.Borrado = 0
              AND v.NombreCliente IS NOT NULL
              AND v.Estatus NOT IN (9)
              AND v.Total IS NOT NULL
              AND ABS(v.Total - ISNULL((
                  SELECT SUM(ISNULL(d.Importe, 0))
                  FROM ventas.VentaDetalle d WHERE d.VentaId = v.Id
              ), 0)) > 0.01";
        var vtaTotalDrift = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        r.Add(vtaTotalDrift > 0
            ? new(AuditSeverity.Error, "Totales Financieros",
                $"{vtaTotalDrift} venta(s) documental(es) con Total en encabezado diferente al SUM de detalles.",
                "Recalcular totales en ventas.Venta para los documentos afectados.",
                ModVta)
            : new(AuditSeverity.Info, "Totales Financieros",
                "Ventas: Total encabezado = SUM(detalles) en todos los activos.", null, ModVta));

        // B3: Cotizacion.Subtotal != SUM(CotizacionDetalle.Importe)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Cotizacion c
            WHERE c.Borrado = 0
              AND c.Estatus NOT IN (4, 9)
              AND c.Subtotal IS NOT NULL
              AND ABS(c.Subtotal - ISNULL((
                  SELECT SUM(d.Importe)
                  FROM ventas.CotizacionDetalle d WHERE d.CotizacionId = c.Id
              ), 0)) > 0.01";
        var cotSubtotalDrift = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        r.Add(cotSubtotalDrift > 0
            ? new(AuditSeverity.Error, "Totales Financieros",
                $"{cotSubtotalDrift} cotización(es) activa(s) con Subtotal en encabezado diferente al SUM de detalles.",
                "Recalcular: UPDATE ventas.Cotizacion SET Subtotal=(SELECT SUM(Importe) FROM ventas.CotizacionDetalle WHERE CotizacionId=Id) WHERE...",
                ModCot)
            : new(AuditSeverity.Info, "Totales Financieros",
                "Cotizaciones: Subtotal encabezado = SUM(detalles).", null, ModCot));

        // B4: Cotizacion.Total != Subtotal + SUM(CotizacionCargo.Importe)
        cmd.CommandText = @"
            SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON t.schema_id=s.schema_id
            WHERE s.name='ventas' AND t.name='CotizacionCargo'";
        var hasCargos = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0) > 0;

        if (hasCargos)
        {
            cmd.CommandText = @"
                SELECT COUNT(*)
                FROM ventas.Cotizacion c
                WHERE c.Borrado = 0
                  AND c.Estatus NOT IN (4, 9)
                  AND c.Subtotal IS NOT NULL
                  AND ABS(c.Total - (c.Subtotal + ISNULL((
                      SELECT SUM(cc.Importe)
                      FROM ventas.CotizacionCargo cc WHERE cc.CotizacionId = c.Id
                  ), 0))) > 0.01";
            var cotTotalDrift = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
            r.Add(cotTotalDrift > 0
                ? new(AuditSeverity.Error, "Totales Financieros",
                    $"{cotTotalDrift} cotización(es) con Total ≠ Subtotal + SUM(OtrosCargos).",
                    "Recalcular Total = Subtotal + SUM(CotizacionCargo.Importe) para los afectados.",
                    ModCot)
                : new(AuditSeverity.Info, "Totales Financieros",
                    "Cotizaciones: Total = Subtotal + OtrosCargos (fórmula correcta).", null, ModCot));
        }

        // B5: Detalles con Importe = 0 y Cantidad > 0 (precio cero sospechoso)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.PedidoDetalle d
            JOIN ventas.Pedido p ON d.PedidoId = p.Id
            WHERE p.Borrado = 0 AND p.Estatus NOT IN (9)
              AND d.Cantidad > 0 AND d.Importe = 0 AND d.PrecioUnitario = 0";
        var preciosCero = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (preciosCero > 0)
            r.Add(new(AuditSeverity.Warning, "Totales Financieros",
                $"{preciosCero} línea(s) de Pedido con Cantidad > 0 pero Precio = 0 e Importe = 0.",
                "Verificar si el precio cero es intencional (productos cortesía) o un error de captura.",
                ModPed));

        return r;
    }

    // ════════════════════════════════════════════════════════════════════════
    // C. Integridad de pagos
    // ════════════════════════════════════════════════════════════════════════

    private static async Task<IReadOnlyList<SchemaAuditEntry>> AuditPaymentIntegrityAsync(
        DbConnection conn, CancellationToken ct)
    {
        var r = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // C1: PagoVenta con Monto = 0 o negativo (imposible)
        cmd.CommandText = @"
            SELECT COUNT(*) FROM ventas.PagoVenta WHERE Monto <= 0";
        var pagosInvalidos = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        r.Add(pagosInvalidos > 0
            ? new(AuditSeverity.Critical, "Integridad Pagos",
                $"{pagosInvalidos} PagoVenta(s) con Monto ≤ 0 — registro imposible.",
                "Eliminar o corregir los pagos con monto inválido en ventas.PagoVenta.",
                ModPag)
            : new(AuditSeverity.Info, "Integridad Pagos",
                "PagoVenta: todos los montos son positivos.", null, ModPag));

        // C2: Venta.TotalPagado != SUM(PagoVenta.Monto) — contador desincronizado (Critical)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Venta v
            WHERE v.Borrado = 0
              AND v.NombreCliente IS NOT NULL
              AND v.Estatus NOT IN (0, 9)
              AND ABS(v.TotalPagado - ISNULL((
                  SELECT SUM(p.Monto)
                  FROM ventas.PagoVenta p WHERE p.VentaId = v.Id
              ), 0)) > 0.01";
        var totalPagadoDrift = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        r.Add(totalPagadoDrift > 0
            ? new(AuditSeverity.Critical, "Integridad Pagos",
                $"{totalPagadoDrift} venta(s) con TotalPagado en encabezado ≠ SUM(PagoVenta.Monto) — contador desincronizado.",
                "Recalcular: UPDATE ventas.Venta SET TotalPagado=(SELECT ISNULL(SUM(Monto),0) FROM ventas.PagoVenta WHERE VentaId=Id) WHERE...",
                ModPag)
            : new(AuditSeverity.Info, "Integridad Pagos",
                "TotalPagado = SUM(PagoVenta.Monto) en todos los documentos.", null, ModPag));

        // C3: Ventas Pagadas (2) con SaldoPendiente > 0.01 — inconsistencia de estado
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Venta
            WHERE Borrado = 0
              AND Estatus = 2
              AND NombreCliente IS NOT NULL
              AND Total IS NOT NULL
              AND (Total - TotalPagado) > 0.01";
        var pagadasConSaldo = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        r.Add(pagadasConSaldo > 0
            ? new(AuditSeverity.Error, "Integridad Pagos",
                $"{pagadasConSaldo} venta(s) en estado Pagada con saldo pendiente > 0 — inconsistencia de auto-transición.",
                "La auto-transición PendientePago→Pagada ocurre cuando TotalPagado >= Total. Verificar los montos.",
                ModPag)
            : new(AuditSeverity.Info, "Integridad Pagos",
                "Ventas en estado Pagada: todas con saldo = 0.", null, ModPag));

        // C4: PagoVenta sin FormaPago (campo operacional)
        cmd.CommandText = @"
            SELECT COUNT(*) FROM ventas.PagoVenta
            WHERE FormaPago IS NULL OR FormaPago = ''";
        var pagosSinFormaPago = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (pagosSinFormaPago > 0)
            r.Add(new(AuditSeverity.Warning, "Integridad Pagos",
                $"{pagosSinFormaPago} pago(s) sin FormaPago registrada.",
                "Completar la FormaPago en ventas.PagoVenta para trazabilidad operacional.",
                ModPag));

        // C5: Resumen operacional de pagos
        cmd.CommandText = @"
            SELECT COUNT(*), SUM(Monto)
            FROM ventas.PagoVenta pv
            JOIN ventas.Venta v ON pv.VentaId = v.Id
            WHERE v.Borrado = 0 AND v.NombreCliente IS NOT NULL";
        using (var rdr = await cmd.ExecuteReaderAsync(ct))
        {
            if (await rdr.ReadAsync(ct))
            {
                var totalPagos = rdr.GetInt32(0);
                var sumaPagos  = rdr.IsDBNull(1) ? 0m : rdr.GetDecimal(1);
                r.Add(new(AuditSeverity.Info, "Resumen Pagos",
                    $"Pagos documentales: {totalPagos} registro(s), suma total: {sumaPagos:C2}.",
                    null, ModPag));
            }
        }

        return r;
    }

    // ════════════════════════════════════════════════════════════════════════
    // D. Documentos estancados (aging operacional)
    // ════════════════════════════════════════════════════════════════════════

    private static async Task<IReadOnlyList<SchemaAuditEntry>> AuditDocumentAgingAsync(
        DbConnection conn, CancellationToken ct)
    {
        var r = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // D1: Cotizaciones Borrador > 60 días sin modificar (posiblemente abandonadas)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Cotizacion
            WHERE Borrado = 0 AND Estatus = 0
              AND ISNULL(FechaModificacion, FechaCreacion) < DATEADD(DAY, -60, GETDATE())";
        var cotBorradorViejas = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (cotBorradorViejas > 0)
            r.Add(new(AuditSeverity.Warning, "Aging Operacional",
                $"{cotBorradorViejas} cotización(es) Borrador sin modificar por más de 60 días.",
                "Revisar si deben aprobarse, cancelarse o están activas pero sin actividad reciente.",
                ModCot));

        // D2: Pedidos Borrador > 30 días sin avanzar
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Pedido
            WHERE Borrado = 0 AND Estatus = 0
              AND ISNULL(FechaModificacion, FechaCreacion) < DATEADD(DAY, -30, GETDATE())";
        var pedBorradorViejos = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (pedBorradorViejos > 0)
            r.Add(new(AuditSeverity.Warning, "Aging Operacional",
                $"{pedBorradorViejos} pedido(s) Borrador sin avanzar por más de 30 días.",
                "Verificar si deben autorizarse (estado Autorizado) o cancelarse.",
                ModPed));

        // D3: Ventas Borrador > 7 días sin confirmar
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Venta
            WHERE Borrado = 0 AND Estatus = 0
              AND NombreCliente IS NOT NULL
              AND ISNULL(FechaModificacion, DATEADD(DAY, -8, GETDATE())) < DATEADD(DAY, -7, GETDATE())";
        var vtaBorradorViejas = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (vtaBorradorViejas > 0)
            r.Add(new(AuditSeverity.Warning, "Aging Operacional",
                $"{vtaBorradorViejas} venta(s) Borrador sin confirmar por más de 7 días.",
                "Verificar y confirmar o cancelar estas ventas en estado Borrador.",
                ModVta));

        // D4: Ventas PendientePago > 90 días sin pago registrado
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Venta v
            WHERE v.Borrado = 0 AND v.Estatus = 1
              AND v.NombreCliente IS NOT NULL
              AND v.TotalPagado = 0
              AND v.Fecha < DATEADD(DAY, -90, GETDATE())";
        var vtaSinPagoViejas = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (vtaSinPagoViejas > 0)
            r.Add(new(AuditSeverity.Warning, "Aging Operacional",
                $"{vtaSinPagoViejas} venta(s) PendientePago > 90 días sin ningún pago registrado.",
                "Gestionar cobro o cerrar cuenta. Verificar si deben generar CxC.",
                ModVta));

        // D5: Resumen de distribución de estados (info operacional)
        cmd.CommandText = @"
            SELECT Estatus, COUNT(*) as Total
            FROM ventas.Cotizacion WHERE Borrado=0
            GROUP BY Estatus ORDER BY Estatus";
        var estadosCot = new List<string>();
        using (var rdr = await cmd.ExecuteReaderAsync(ct))
        {
            while (await rdr.ReadAsync(ct))
            {
                var est = rdr.GetInt32(0);
                var label = est switch { 0=>"Borrador",2=>"Aprobada",3=>"Convertida",9=>"Cancelada",_=>$"Estatus={est}" };
                estadosCot.Add($"{label}:{rdr.GetInt32(1)}");
            }
        }
        if (estadosCot.Any())
            r.Add(new(AuditSeverity.Info, "Distribución Estados",
                $"Cotizaciones por estado: {string.Join(", ", estadosCot)}", null, ModCot));

        cmd.CommandText = @"
            SELECT Estatus, COUNT(*) as Total
            FROM ventas.Pedido WHERE Borrado=0
            GROUP BY Estatus ORDER BY Estatus";
        var estadosPed = new List<string>();
        using (var rdr = await cmd.ExecuteReaderAsync(ct))
        {
            while (await rdr.ReadAsync(ct))
            {
                var est = rdr.GetInt32(0);
                var label = est switch { 0=>"Borrador",1=>"Autorizado",2=>"EnProceso",3=>"Finalizado",4=>"Parcial",9=>"Cancelado",_=>$"Estatus={est}" };
                estadosPed.Add($"{label}:{rdr.GetInt32(1)}");
            }
        }
        if (estadosPed.Any())
            r.Add(new(AuditSeverity.Info, "Distribución Estados",
                $"Pedidos por estado: {string.Join(", ", estadosPed)}", null, ModPed));

        cmd.CommandText = @"
            SELECT Estatus, COUNT(*) as Total
            FROM ventas.Venta WHERE Borrado=0 AND NombreCliente IS NOT NULL
            GROUP BY Estatus ORDER BY Estatus";
        var estadosVta = new List<string>();
        using (var rdr = await cmd.ExecuteReaderAsync(ct))
        {
            while (await rdr.ReadAsync(ct))
            {
                var est = rdr.GetInt32(0);
                var label = est switch { 0=>"Borrador",1=>"PendientePago",2=>"Pagada",3=>"Facturada",4=>"Entregada",5=>"Cerrada",9=>"Cancelada",_=>$"Estatus={est}" };
                estadosVta.Add($"{label}:{rdr.GetInt32(1)}");
            }
        }
        if (estadosVta.Any())
            r.Add(new(AuditSeverity.Info, "Distribución Estados",
                $"Ventas documentales por estado: {string.Join(", ", estadosVta)}", null, ModVta));

        return r;
    }

    // ════════════════════════════════════════════════════════════════════════
    // E. Integridad de referencias cruzadas de productos
    // ════════════════════════════════════════════════════════════════════════

    private static async Task<IReadOnlyList<SchemaAuditEntry>> AuditProductReferencesAsync(
        DbConnection conn, CancellationToken ct)
    {
        var r = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // E1: CotizacionDetalle.ProductoId → Producto eliminado (soft-delete)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.CotizacionDetalle cd
            JOIN ventas.Cotizacion c ON cd.CotizacionId = c.Id
            WHERE c.Borrado = 0 AND c.Estatus NOT IN (4, 9)
              AND cd.ProductoId IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM core.Producto p
                  WHERE p.Id = cd.ProductoId AND p.Borrado = 0
              )";
        var cotDetProdInvalido = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        r.Add(cotDetProdInvalido > 0
            ? new(AuditSeverity.Warning, "Referencias Productos",
                $"{cotDetProdInvalido} línea(s) de Cotización activa referenciando un Producto eliminado.",
                "El Producto fue eliminado después de crear la cotización. El snapshot de Descripcion lo preserva.",
                ModCot)
            : new(AuditSeverity.Info, "Referencias Productos",
                "CotizacionDetalle: todas las referencias a Producto son válidas en cotizaciones activas.", null, ModCot));

        // E2: PedidoDetalle.ProductoId → Producto eliminado
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.PedidoDetalle pd
            JOIN ventas.Pedido p ON pd.PedidoId = p.Id
            WHERE p.Borrado = 0 AND p.Estatus NOT IN (9)
              AND pd.ProductoId IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM core.Producto pr
                  WHERE pr.Id = pd.ProductoId AND pr.Borrado = 0
              )";
        var pedDetProdInvalido = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        r.Add(pedDetProdInvalido > 0
            ? new(AuditSeverity.Warning, "Referencias Productos",
                $"{pedDetProdInvalido} línea(s) de Pedido activo referenciando un Producto eliminado.",
                "El Producto fue eliminado. El snapshot en PedidoDetalle.Descripcion lo preserva.",
                ModPed)
            : new(AuditSeverity.Info, "Referencias Productos",
                "PedidoDetalle: todas las referencias a Producto son válidas en pedidos activos.", null, ModPed));

        // E3: VentaDetalle.ProductoId → Producto eliminado o ProductoId = 0
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.VentaDetalle vd
            JOIN ventas.Venta v ON vd.VentaId = v.Id
            WHERE v.Borrado = 0 AND v.NombreCliente IS NOT NULL
              AND v.Estatus NOT IN (9)
              AND vd.ProductoId > 0
              AND NOT EXISTS (
                  SELECT 1 FROM core.Producto p
                  WHERE p.Id = vd.ProductoId AND p.Borrado = 0
              )";
        var vtaDetProdInvalido = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        r.Add(vtaDetProdInvalido > 0
            ? new(AuditSeverity.Warning, "Referencias Productos",
                $"{vtaDetProdInvalido} línea(s) de Venta activa referenciando un Producto eliminado.",
                "Producto eliminado posterior a la venta. El nombre puede perderse al mostrar (usar Include Producto).",
                ModVta)
            : new(AuditSeverity.Info, "Referencias Productos",
                "VentaDetalle: todas las referencias a Producto son válidas en ventas activas.", null, ModVta));

        return r;
    }

    // ════════════════════════════════════════════════════════════════════════
    // F. Crédito y CxC — coherencia
    // ════════════════════════════════════════════════════════════════════════

    private static async Task<IReadOnlyList<SchemaAuditEntry>> AuditCreditAndCxCAsync(
        DbConnection conn, CancellationToken ct)
    {
        var r = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // F1: Ventas de crédito confirmadas sin CxC correspondiente
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Venta v
            WHERE v.Borrado = 0
              AND v.NombreCliente IS NOT NULL
              AND v.TipoPago = 1
              AND v.Estatus NOT IN (0, 9)
              AND NOT EXISTS (
                  SELECT 1 FROM finanzas.CuentaPorCobrar cxc
                  WHERE cxc.Concepto = 'Venta #' + CAST(v.Id AS NVARCHAR)
                    AND cxc.EmpresaId = v.EmpresaId
              )";
        var vtaCreditoSinCxC = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        r.Add(vtaCreditoSinCxC > 0
            ? new(AuditSeverity.Error, "Crédito y CxC",
                $"{vtaCreditoSinCxC} venta(s) de crédito confirmada(s) sin CuentaPorCobrar correspondiente.",
                "Verificar VentaDocumentalService.ConfirmarAsync — debe generar CxC si TipoPago=Crédito.",
                ModCxC)
            : new(AuditSeverity.Info, "Crédito y CxC",
                "Ventas de crédito: todas tienen CxC generada.", null, ModCxC));

        // F2: CxC con Concepto "Venta #X" donde la Venta está cancelada
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM finanzas.CuentaPorCobrar cxc
            JOIN ventas.Venta v
                ON cxc.Concepto = 'Venta #' + CAST(v.Id AS NVARCHAR)
               AND cxc.EmpresaId = v.EmpresaId
            WHERE v.Borrado = 0 AND v.Estatus = 9
              AND cxc.Borrado = 0";
        var cxcParaVentasCanceladas = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (cxcParaVentasCanceladas > 0)
            r.Add(new(AuditSeverity.Warning, "Crédito y CxC",
                $"{cxcParaVentasCanceladas} CxC activa(s) referenciando venta(s) cancelada(s).",
                "Verificar si las CxC deben cancelarse o saldarse cuando la venta se cancela.",
                ModCxC));

        // F3: Ventas de crédito Pagadas (2) sin CxC saldada
        // SaldoPendiente = MontoOriginal - MontoPagado (NO es columna persistida — calculado)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Venta v
            JOIN finanzas.CuentaPorCobrar cxc
                ON cxc.Concepto = 'Venta #' + CAST(v.Id AS NVARCHAR)
               AND cxc.EmpresaId = v.EmpresaId
            WHERE v.Borrado = 0
              AND v.TipoPago = 1
              AND v.Estatus = 2
              AND v.NombreCliente IS NOT NULL
              AND cxc.Borrado = 0
              AND (cxc.MontoOriginal - cxc.MontoPagado) > 0.01";
        var vtaPagadaConCxCPendiente = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (vtaPagadaConCxCPendiente > 0)
            r.Add(new(AuditSeverity.Warning, "Crédito y CxC",
                $"{vtaPagadaConCxCPendiente} venta(s) Pagada con CxC que aún tiene saldo > 0.",
                "El pago de la Venta debería actualizar la CxC correspondiente. Verificar RegistrarPagoAsync.",
                ModCxC));

        return r;
    }

    // ════════════════════════════════════════════════════════════════════════
    // G. Audit trail readiness
    // ════════════════════════════════════════════════════════════════════════

    private static async Task<IReadOnlyList<SchemaAuditEntry>> AuditTrailReadinessAsync(
        DbConnection conn, CancellationToken ct)
    {
        var r = new List<SchemaAuditEntry>();
        using var cmd = conn.CreateCommand();

        // G1: Detectar si existe alguna tabla de audit trail
        var auditTables = new[] { "AuditLog", "EventLog", "DocumentoHistorial", "BitacoraDocumento" };
        var found = new List<string>();

        foreach (var table in auditTables)
        {
            cmd.CommandText = @"
                SELECT COUNT(*) FROM sys.tables t
                JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE t.name = @name";
            cmd.Parameters.Clear();
            var p = cmd.CreateParameter(); p.ParameterName = "@name"; p.Value = table; cmd.Parameters.Add(p);
            if ((int)(await cmd.ExecuteScalarAsync(ct) ?? 0) > 0)
                found.Add(table);
        }

        cmd.Parameters.Clear();

        if (found.Count > 0)
        {
            r.Add(new(AuditSeverity.Info, "Audit Trail",
                $"Tablas de audit trail detectadas: {string.Join(", ", found)}.",
                null, ModGen));
        }
        else
        {
            r.Add(new(AuditSeverity.Info, "Audit Trail",
                "Sin infraestructura de audit trail detectada — pendiente de implementación futura.",
                "Implementar tabla de historial documental para trazabilidad: Creado, Aprobado, Convertido, Pagado, Cerrado.",
                ModGen));
        }

        // G2: Verificar que FechaCreacion/FechaModificacion están poblados en documentos recientes
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Cotizacion
            WHERE Borrado = 0 AND FechaCreacion IS NULL";
        var cotSinFechaCreacion = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (cotSinFechaCreacion > 0)
            r.Add(new(AuditSeverity.Warning, "Audit Trail",
                $"{cotSinFechaCreacion} cotización(es) sin FechaCreacion — campos de auditoría incompletos.",
                "FechaCreacion debería poblarse automáticamente vía AuditableEntity.FechaCreacion.",
                ModCot));

        // G3: UsuarioCreacionId = null en documentos recientes (sin trazabilidad de usuario)
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM ventas.Cotizacion
            WHERE Borrado = 0 AND UsuarioCreacionId IS NULL
              AND FechaCreacion > DATEADD(DAY, -30, GETDATE())";
        var cotSinUsuario = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (cotSinUsuario > 0)
            r.Add(new(AuditSeverity.Warning, "Audit Trail",
                $"{cotSinUsuario} cotización(es) reciente(s) sin UsuarioCreacionId — trazabilidad incompleta.",
                "Verificar que el SessionService.UsuarioId esté correctamente asignado al crear documentos.",
                ModCot));
        else
            r.Add(new(AuditSeverity.Info, "Audit Trail",
                "Cotizaciones recientes: todas con UsuarioCreacionId registrado.", null, ModCot));

        return r;
    }
}

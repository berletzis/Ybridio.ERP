namespace Ybridio.Infrastructure.Persistence.Audit;

/// <summary>
/// Servicio de auditoría de consistencia estructural.
/// Compara el modelo EF Core (fuente de verdad) con la estructura real de la base de datos
/// y genera un reporte con errores y advertencias.
/// <para>
/// <b>Fuente de verdad:</b> Domain + EF Configurations.
/// La base de datos debe alinearse al modelo, nunca al revés.
/// </para>
/// <para>
/// <b>Uso típico (modo DEBUG en startup):</b>
/// <code>
/// var audit = serviceProvider.GetRequiredService&lt;ISchemaAuditService&gt;();
/// var report = await audit.RunAsync();
/// report.PrintToConsole();
/// </code>
/// </para>
/// </summary>
public interface ISchemaAuditService
{
    /// <summary>
    /// Ejecuta la auditoría completa: migraciones pendientes, tablas, columnas y relaciones.
    /// Solo lee — nunca modifica datos ni estructura.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// <see cref="SchemaAuditReport"/> con todos los hallazgos clasificados por severidad.
    /// </returns>
    Task<SchemaAuditReport> RunAsync(CancellationToken ct = default);
}

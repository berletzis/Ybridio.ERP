namespace Ybridio.Infrastructure.Persistence.Audit;

/// <summary>
/// ERP Structural Integrity Engine — validación de integridad workflow/lifecycle/snapshot.
/// Audita el estado semántico de los documentos comerciales y clasifica hallazgos con
/// awareness de: workflow actual, datos legacy, migraciones manuales y snapshots documentales.
/// <para>
/// Complementa a <see cref="ISchemaAuditService"/> (estructura) y
/// <see cref="IDatabaseAuditService"/> (datos catálogos).
/// Este servicio se enfoca en la integridad del workflow comercial.
/// </para>
/// </summary>
public interface IWorkflowAuditService
{
    /// <summary>
    /// Ejecuta la auditoría integral de workflow comercial.
    /// Incluye: lifecycle documental, snapshots, clasificación legacy, migraciones manuales.
    /// Solo lee — nunca modifica datos.
    /// </summary>
    Task<SchemaAuditReport> RunAsync(CancellationToken ct = default);
}

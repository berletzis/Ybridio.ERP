namespace Ybridio.Infrastructure.Persistence.Audit;

/// <summary>
/// Auditoría de integridad de datos post-migración dbo → catalogos.
/// Valida duplicidad funcional, FK rotas, consistencia de catálogos
/// y dependencias residuales hacia el schema dbo legacy.
/// A diferencia de <see cref="ISchemaAuditService"/> (que compara el modelo EF
/// con la estructura de la BD), este servicio valida los DATOS y las
/// relaciones entre tablas ya existentes.
/// Solo lee — nunca modifica datos ni estructura.
/// </summary>
public interface IDatabaseAuditService
{
    /// <summary>
    /// Ejecuta la auditoría integral de datos y relaciones.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// <see cref="SchemaAuditReport"/> con todos los hallazgos clasificados
    /// por severidad (Critical → Error → Warning → Info).
    /// </returns>
    Task<SchemaAuditReport> RunAsync(CancellationToken ct = default);
}

namespace Ybridio.Infrastructure.Persistence.Audit;

/// <summary>
/// Auditoría de integridad comercial — valida la coherencia del workflow completo:
/// Cotización → Pedido → Venta → Pago → Cierre.
/// <para>
/// Se enfoca en la consistencia FINANCIERA y la COHERENCIA ENTRE DOCUMENTOS,
/// complementando a <see cref="IWorkflowAuditService"/> que valida el lifecycle
/// individual de cada documento.
/// </para>
/// <para>
/// Módulos cubiertos: Cotizaciones, Pedidos, Ventas, Pagos, CxC.
/// </para>
/// </summary>
public interface ICommercialIntegrityAuditService
{
    /// <summary>
    /// Ejecuta la auditoría de integridad comercial completa.
    /// Valida: cadena conversión, totales, pagos, aging, referencias cruzadas,
    /// crédito/CxC y audit trail readiness.
    /// Solo lee — nunca modifica datos.
    /// </summary>
    Task<SchemaAuditReport> RunAsync(CancellationToken ct = default);
}

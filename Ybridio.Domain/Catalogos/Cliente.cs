using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Catalogos;

/// <summary>
/// Cliente de la empresa. Entidad core preparada para CRM ligero.
/// Soporta ventas simples, cotizaciones, pedidos y órdenes de trabajo.
/// </summary>
/// <remarks>
/// LimiteCredito = 0 significa que el cliente no tiene crédito habilitado (pago de contado).
/// SaldoPendiente no se persiste — se calcula en runtime desde CuentaPorCobrar (ver §25 CLAUDE_RULES.md).
/// La entidad vive en schema catalogos/ pero actúa como entidad core del módulo de ventas.
/// </remarks>
public class Cliente : AuditableEntity
{
    public int    Id          { get; set; }
    public int    EmpresaId   { get; set; }
    public string Nombre      { get; set; } = string.Empty;
    public string? RFC        { get; set; }
    public string? Email      { get; set; }

    // ── Campos extendidos para Sales Core ─────────────────────────────────────
    public string? Telefono   { get; set; }
    public string? Direccion  { get; set; }
    public string? Notas      { get; set; }

    /// <summary>
    /// Límite de crédito en moneda local.
    /// Fórmula de validación runtime: SaldoPendiente_CxC ≤ LimiteCredito.
    /// 0 = sin crédito (contado únicamente).
    /// No persiste el saldo — se calcula desde CuentaPorCobrar al consultar.
    /// </summary>
    public decimal LimiteCredito { get; set; }

    // Navegación
    public Empresa Empresa { get; set; } = null!;
}

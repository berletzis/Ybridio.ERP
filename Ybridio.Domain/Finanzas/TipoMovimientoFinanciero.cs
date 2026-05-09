namespace Ybridio.Domain.Finanzas;

/// <summary>
/// Discriminador de tipo para <see cref="MovimientoFinanciero"/>.
/// Un gasto reduce el efectivo disponible; un ingreso (no proveniente de ventas) lo incrementa.
/// </summary>
public enum TipoMovimientoFinanciero
{
    /// <summary>Egreso operativo: agua, luz, gasolina, nómina, mantenimiento, etc.</summary>
    Gasto = 1,

    /// <summary>Ingreso no generado por ventas: préstamo, inversión, depósito, recuperación.</summary>
    Ingreso = 2,
}

namespace Ybridio.Domain.Ventas;

/// <summary>Tipo de pago acordado para la venta documental.</summary>
public enum TipoPago
{
    /// <summary>Pago de contado al confirmar. No genera CxC.</summary>
    Contado = 0,
    /// <summary>Crédito: genera CxC automáticamente al confirmar la venta.</summary>
    Credito = 1,
}

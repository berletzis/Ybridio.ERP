namespace Ybridio.Application.DTOs.Finanzas;

/// <summary>
/// DTO de lectura para una cuenta por cobrar.
/// <see cref="SaldoPendiente"/> = MontoOriginal - MontoPagado (calculado, no almacenado).
/// <see cref="EsVencida"/> = FechaVencimiento &lt; hoy Y SaldoPendiente &gt; 0.
/// </summary>
public sealed record CxCDto(
    long     Id,
    int      EmpresaId,
    int?     SucursalId,
    string   NombreDeudor,
    string   Concepto,
    decimal  MontoOriginal,
    decimal  MontoPagado,
    decimal  SaldoPendiente,
    DateTime FechaEmision,
    DateTime FechaVencimiento,
    bool     EsVencida,
    string?  Observaciones);

/// <summary>DTO para crear una cuenta por cobrar.</summary>
public sealed record CrearCxCDto(
    int      EmpresaId,
    int?     SucursalId,
    string   NombreDeudor,
    string   Concepto,
    decimal  MontoOriginal,
    DateTime FechaEmision,
    DateTime FechaVencimiento,
    string?  Observaciones);

/// <summary>DTO para registrar un pago parcial o total de CxC.</summary>
public sealed record RegistrarPagoCxCDto(
    long    CxCId,
    decimal Monto);

namespace Ybridio.Application.DTOs.Finanzas;

/// <summary>
/// DTO de lectura para una cuenta por pagar.
/// <see cref="SaldoPendiente"/> = MontoOriginal - MontoPagado (calculado, no almacenado).
/// <see cref="EsVencida"/> = FechaVencimiento &lt; hoy Y SaldoPendiente &gt; 0.
/// </summary>
public sealed record CxPDto(
    long     Id,
    int      EmpresaId,
    int?     SucursalId,
    string   NombreAcreedor,
    string   Concepto,
    decimal  MontoOriginal,
    decimal  MontoPagado,
    decimal  SaldoPendiente,
    DateTime FechaEmision,
    DateTime FechaVencimiento,
    bool     EsVencida,
    string?  Observaciones);

/// <summary>DTO para crear una cuenta por pagar.</summary>
public sealed record CrearCxPDto(
    int      EmpresaId,
    int?     SucursalId,
    string   NombreAcreedor,
    string   Concepto,
    decimal  MontoOriginal,
    DateTime FechaEmision,
    DateTime FechaVencimiento,
    string?  Observaciones);

/// <summary>DTO para registrar un pago parcial o total de CxP.</summary>
public sealed record RegistrarPagoCxPDto(
    long    CxPId,
    decimal Monto);

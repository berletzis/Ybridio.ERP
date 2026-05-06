namespace Ybridio.Application.DTOs.Finanzas;

/// <summary>DTO de lectura para Caja.</summary>
public sealed record CajaDto(
    int Id,
    int EmpresaId,
    int SucursalId,
    string Nombre,
    decimal Saldo);

/// <summary>DTO de lectura para AperturaCaja.</summary>
public sealed record AperturaCajaDto(
    int Id,
    int CajaId,
    string CajaNombre,
    Guid UsuarioId,
    DateTime FechaApertura,
    DateTime? FechaCierre,
    decimal MontoInicial,
    decimal? MontoFinal,
    bool Activa);

/// <summary>DTO de entrada para abrir una caja.</summary>
public sealed record AbrirCajaDto(
    int CajaId,
    Guid UsuarioId,
    decimal MontoInicial);

/// <summary>DTO de entrada para cerrar una caja.</summary>
public sealed record CerrarCajaDto(
    int AperturaCajaId,
    decimal MontoFinal);

/// <summary>DTO de lectura para MovimientoCaja.</summary>
public sealed record MovimientoCajaDto(
    long Id,
    int CajaId,
    int? TipoMovimientoId,
    string? TipoMovimientoNombre,
    decimal Monto,
    DateTime Fecha,
    string? Referencia);

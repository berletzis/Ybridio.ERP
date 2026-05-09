using Ybridio.Domain.Finanzas;

namespace Ybridio.Application.DTOs.Finanzas;

/// <summary>
/// DTO de lectura para un movimiento financiero (gasto o ingreso operativo).
/// SaldoPendiente no aplica aquí — es solo para CxC/CxP.
/// </summary>
public sealed record MovimientoFinancieroDto(
    long                        Id,
    int                         EmpresaId,
    int?                        SucursalId,
    TipoMovimientoFinanciero    Tipo,
    ContextoFinanciero          Contexto,
    int?                        CategoriaId,
    string?                     CategoriaNombre,
    string?                     CategoriaColor,
    string                      Concepto,
    decimal                     Monto,
    DateTime                    Fecha,
    string?                     Observaciones);

/// <summary>DTO para crear un gasto o ingreso.</summary>
public sealed record CrearMovimientoFinancieroDto(
    int                         EmpresaId,
    int?                        SucursalId,
    TipoMovimientoFinanciero    Tipo,
    ContextoFinanciero          Contexto,
    int?                        CategoriaId,
    string                      Concepto,
    decimal                     Monto,
    DateTime                    Fecha,
    string?                     Observaciones);

/// <summary>DTO para actualizar un movimiento existente.</summary>
public sealed record ActualizarMovimientoFinancieroDto(
    int?                        CategoriaId,
    string                      Concepto,
    decimal                     Monto,
    DateTime                    Fecha,
    string?                     Observaciones);

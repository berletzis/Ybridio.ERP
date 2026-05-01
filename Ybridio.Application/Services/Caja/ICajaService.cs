using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Finanzas;

namespace Ybridio.Application.Services.Caja;

/// <summary>
/// Contrato para la gestión de cajas POS.
/// </summary>
public interface ICajaService
{
    /// <summary>Abre una caja para el usuario indicado.</summary>
    /// <remarks>Falla si la caja ya tiene una apertura activa.</remarks>
    Task<ServiceResult<AperturaCajaDto>> AbrirCajaAsync(AbrirCajaDto dto, CancellationToken ct = default);

    /// <summary>Cierra la apertura de caja indicada registrando el monto final.</summary>
    Task<ServiceResult<AperturaCajaDto>> CerrarCajaAsync(CerrarCajaDto dto, CancellationToken ct = default);

    /// <summary>
    /// Retorna la apertura de caja activa para el usuario indicado, o falla si no existe.
    /// </summary>
    Task<ServiceResult<AperturaCajaDto>> ObtenerCajaActivaAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>Lista todas las cajas de una empresa.</summary>
    Task<IReadOnlyList<CajaDto>> ListarCajasPorEmpresaAsync(int empresaId, CancellationToken ct = default);
}

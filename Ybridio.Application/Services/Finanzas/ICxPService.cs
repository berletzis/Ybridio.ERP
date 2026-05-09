using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Finanzas;

namespace Ybridio.Application.Services.Finanzas;

/// <summary>
/// Contrato para gestión de cuentas por pagar con enforcement de autorización.
/// </summary>
public interface ICxPService
{
    /// <summary>Lista CxP de la empresa. Valida cxp.ver.</summary>
    Task<ServiceResult<IReadOnlyList<CxPDto>>> ListarAsync(
        int               empresaId,
        int?              sucursalId    = null,
        bool              soloVigentes  = false,
        DateTime?         desde         = null,
        DateTime?         hasta         = null,
        CancellationToken ct            = default);

    /// <summary>Crea una CxP nueva. Valida cxp.crear.</summary>
    Task<ServiceResult<CxPDto>> CrearAsync(
        CrearCxPDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Registra un pago parcial o total. Valida cxp.editar.
    /// Si el monto pagado llega a MontoOriginal, el registro queda liquidado.
    /// </summary>
    Task<ServiceResult<CxPDto>> RegistrarPagoAsync(
        RegistrarPagoCxPDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Soft-delete de una CxP. Valida cxp.editar.</summary>
    Task<ServiceResult> EliminarAsync(long id, Guid usuarioId, CancellationToken ct = default);
}

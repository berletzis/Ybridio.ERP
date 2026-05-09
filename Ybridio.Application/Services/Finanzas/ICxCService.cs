using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Finanzas;

namespace Ybridio.Application.Services.Finanzas;

/// <summary>
/// Contrato para gestión de cuentas por cobrar con enforcement de autorización.
/// </summary>
public interface ICxCService
{
    /// <summary>Lista CxC de la empresa. Valida cxc.ver.</summary>
    Task<ServiceResult<IReadOnlyList<CxCDto>>> ListarAsync(
        int               empresaId,
        int?              sucursalId    = null,
        bool              soloVigentes  = false,
        DateTime?         desde         = null,
        DateTime?         hasta         = null,
        CancellationToken ct            = default);

    /// <summary>Crea una CxC nueva. Valida cxc.crear.</summary>
    Task<ServiceResult<CxCDto>> CrearAsync(
        CrearCxCDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Registra un pago parcial o total. Valida cxc.editar.
    /// Si el monto pagado llega a MontoOriginal, el registro queda liquidado.
    /// </summary>
    Task<ServiceResult<CxCDto>> RegistrarPagoAsync(
        RegistrarPagoCxCDto dto, Guid usuarioId, CancellationToken ct = default);

    /// <summary>Soft-delete de una CxC. Valida cxc.editar.</summary>
    Task<ServiceResult> EliminarAsync(long id, Guid usuarioId, CancellationToken ct = default);
}

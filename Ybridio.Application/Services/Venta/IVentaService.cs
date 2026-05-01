using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Ventas;

namespace Ybridio.Application.Services.Venta;

/// <summary>
/// Contrato para el proceso de venta en el POS.
/// </summary>
public interface IVentaService
{
    /// <summary>
    /// Registra una venta completa dentro de una transacción:
    /// valida stock, crea Venta + VentaDetalle, descuenta inventario y registra movimiento de caja.
    /// </summary>
    Task<ServiceResult<VentaDto>> CrearVentaAsync(
        RegistrarVentaDto dto,
        Guid usuarioId,
        CancellationToken ct = default);

    /// <summary>Obtiene una venta por su ID incluyendo detalles.</summary>
    Task<ServiceResult<VentaDto>> ObtenerPorIdAsync(long ventaId, CancellationToken ct = default);

    /// <summary>Lista las ventas de una empresa dentro de un rango de fechas.</summary>
    Task<IReadOnlyList<VentaDto>> ListarPorEmpresaAsync(
        int empresaId,
        DateTime desde,
        DateTime hasta,
        CancellationToken ct = default);
}

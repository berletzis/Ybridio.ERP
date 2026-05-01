using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Finanzas;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Caja;

/// <summary>
/// Gestión de cajas POS: apertura, cierre y consulta de estado.
/// </summary>
public sealed class CajaService : ICajaService
{
    private readonly ErpDbContext _context;
    private readonly ILogger<CajaService> _logger;

    public CajaService(ErpDbContext context, ILogger<CajaService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<AperturaCajaDto>> AbrirCajaAsync(
        AbrirCajaDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var opId = OperationContext.CurrentId;
        _logger.LogInformation(
            "{OperationId} Abriendo caja {CajaId} Usuario:{UsuarioId}.",
            opId, dto.CajaId, dto.UsuarioId);

        try
        {
            var caja = await _context.Cajas
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == dto.CajaId, ct);

            if (caja is null)
            {
                _logger.LogWarning("{OperationId} Caja {CajaId} no encontrada.", opId, dto.CajaId);
                return ServiceResult<AperturaCajaDto>.Fail(
                    $"Caja {dto.CajaId} no encontrada.",
                    ErrorCode.CajaNotFound);
            }

            var perteneceATienda = await _context.UsuariosTiendas
                .AsNoTracking()
                .AnyAsync(ut => ut.UsuarioId == dto.UsuarioId && ut.TiendaId == caja.TiendaId, ct);

            if (!perteneceATienda)
            {
                _logger.LogWarning(
                    "{OperationId} Usuario {UsuarioId} no pertenece a tienda {TiendaId} de caja {CajaId}.",
                    opId, dto.UsuarioId, caja.TiendaId, dto.CajaId);
                return ServiceResult<AperturaCajaDto>.Fail(
                    "El usuario no pertenece a la tienda de esta caja.",
                    ErrorCode.CajaTiendaMismatch);
            }

            var yaAbierta = await _context.AperturasCaja
                .AnyAsync(a => a.CajaId == dto.CajaId && a.Activa, ct);

            if (yaAbierta)
            {
                _logger.LogWarning("{OperationId} Caja {CajaId} ya tiene apertura activa.", opId, dto.CajaId);
                return ServiceResult<AperturaCajaDto>.Fail(
                    "La caja ya tiene una apertura activa.",
                    ErrorCode.CajaAlreadyOpen);
            }

            var ahora = DateTime.UtcNow;
            var apertura = new Domain.Finanzas.AperturaCaja
            {
                CajaId = dto.CajaId,
                UsuarioId = dto.UsuarioId,
                FechaApertura = ahora,
                MontoInicial = dto.MontoInicial,
                Activa = true,
                FechaCreacion = ahora,
                UsuarioCreacionId = dto.UsuarioId,
                Borrado = false
            };

            _context.AperturasCaja.Add(apertura);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "{OperationId} Caja {CajaId} abierta. Apertura:{AperturaId} Usuario:{UsuarioId}.",
                opId, dto.CajaId, apertura.Id, dto.UsuarioId);

            return ServiceResult<AperturaCajaDto>.Ok(MapToDto(apertura, caja.Nombre));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{OperationId} Error inesperado al abrir caja {CajaId}.", opId, dto.CajaId);
            return ServiceResult<AperturaCajaDto>.Fail("Error inesperado al abrir la caja.", ErrorCode.Unknown);
        }
        finally
        {
            OperationContext.Clear();
        }
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<AperturaCajaDto>> CerrarCajaAsync(
        CerrarCajaDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var opId = OperationContext.CurrentId;
        _logger.LogInformation(
            "{OperationId} Cerrando apertura {AperturaId}.", opId, dto.AperturaCajaId);

        try
        {
            var apertura = await _context.AperturasCaja
                .Include(a => a.Caja)
                .FirstOrDefaultAsync(a => a.Id == dto.AperturaCajaId, ct);

            if (apertura is null)
            {
                _logger.LogWarning("{OperationId} Apertura {AperturaId} no encontrada.", opId, dto.AperturaCajaId);
                return ServiceResult<AperturaCajaDto>.Fail(
                    "Apertura de caja no encontrada.",
                    ErrorCode.CajaNotFound);
            }

            if (!apertura.Activa)
            {
                _logger.LogWarning("{OperationId} Apertura {AperturaId} ya está cerrada.", opId, dto.AperturaCajaId);
                return ServiceResult<AperturaCajaDto>.Fail(
                    "La caja ya está cerrada.",
                    ErrorCode.CajaAlreadyClosed);
            }

            apertura.FechaCierre = DateTime.UtcNow;
            apertura.MontoFinal = dto.MontoFinal;
            apertura.Activa = false;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "{OperationId} Apertura {AperturaId} cerrada. MontoFinal:{MontoFinal}.",
                opId, apertura.Id, dto.MontoFinal);

            return ServiceResult<AperturaCajaDto>.Ok(MapToDto(apertura, apertura.Caja.Nombre));
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "{OperationId} Conflicto de concurrencia al cerrar apertura {AperturaId}.",
                opId, dto.AperturaCajaId);
            return ServiceResult<AperturaCajaDto>.Fail(
                "La apertura fue modificada por otro proceso. Recarga e intenta de nuevo.",
                ErrorCode.ConcurrencyConflict);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "{OperationId} Error inesperado al cerrar apertura {AperturaId}.",
                opId, dto.AperturaCajaId);
            return ServiceResult<AperturaCajaDto>.Fail("Error inesperado al cerrar la caja.", ErrorCode.Unknown);
        }
        finally
        {
            OperationContext.Clear();
        }
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<AperturaCajaDto>> ObtenerCajaActivaAsync(
        Guid usuarioId, CancellationToken ct = default)
    {
        var apertura = await _context.AperturasCaja
            .AsNoTracking()
            .Include(a => a.Caja)
            .FirstOrDefaultAsync(a => a.UsuarioId == usuarioId && a.Activa, ct);

        if (apertura is null)
            return ServiceResult<AperturaCajaDto>.Fail(
                "No hay una caja abierta para este usuario.",
                ErrorCode.CajaNotOpen);

        return ServiceResult<AperturaCajaDto>.Ok(MapToDto(apertura, apertura.Caja.Nombre));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CajaDto>> ListarCajasPorEmpresaAsync(
        int empresaId, CancellationToken ct = default)
    {
        return await _context.Cajas
            .AsNoTracking()
            .Where(c => c.EmpresaId == empresaId)
            .Select(c => new CajaDto(c.Id, c.EmpresaId, c.TiendaId, c.Nombre, c.Saldo))
            .ToListAsync(ct);
    }

    // ── mapeo interno ──────────────────────────────────────────────────────────

    private static AperturaCajaDto MapToDto(Domain.Finanzas.AperturaCaja a, string cajaNombre) =>
        new(a.Id, a.CajaId, cajaNombre, a.UsuarioId, a.FechaApertura,
            a.FechaCierre, a.MontoInicial, a.MontoFinal, a.Activa);
}

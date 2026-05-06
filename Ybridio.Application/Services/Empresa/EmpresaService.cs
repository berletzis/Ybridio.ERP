using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Core;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Empresa;

public sealed class EmpresaService : IEmpresaService
{
    private readonly ErpDbContext _context;

    public EmpresaService(ErpDbContext context) => _context = context;

    public async Task<ServiceResult<EmpresaDto>> ObtenerPorIdAsync(
        int empresaId, CancellationToken ct = default)
    {
        var e = await _context.Empresas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == empresaId, ct);

        if (e is null)
            return ServiceResult<EmpresaDto>.Fail("Empresa no encontrada.", ErrorCode.NotFound);

        return ServiceResult<EmpresaDto>.Ok(new EmpresaDto(e.Id, e.Nombre, e.RFC));
    }

    public async Task<ServiceResult<EmpresaDto>> ActualizarAsync(
        int empresaId, UpsertEmpresaDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        var e = await _context.Empresas.FirstOrDefaultAsync(x => x.Id == empresaId, ct);

        if (e is null)
            return ServiceResult<EmpresaDto>.Fail("Empresa no encontrada.", ErrorCode.NotFound);

        e.Nombre             = dto.Nombre.Trim();
        e.RFC                = string.IsNullOrWhiteSpace(dto.RFC) ? null : dto.RFC.Trim();
        e.FechaModificacion  = DateTime.UtcNow;
        e.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<EmpresaDto>.Ok(new EmpresaDto(e.Id, e.Nombre, e.RFC));
    }
}

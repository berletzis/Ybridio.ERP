using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Directorio;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Domain.Catalogos;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Directorio;

/// <summary>
/// Gestión de EmpresasComerciales del directorio con enforcement de autorización runtime.
/// </summary>
public sealed class EmpresaComercialService : IEmpresaComercialService
{
    private readonly ErpDbContext             _context;
    private readonly IErpAuthorizationService _auth;

    public EmpresaComercialService(ErpDbContext context, IErpAuthorizationService auth)
    {
        _context = context;
        _auth    = auth;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EmpresaComercialDto>> ListarPorEmpresaAsync(int empresaId, CancellationToken ct = default)
    {
        var lista = await _context.EmpresasComerciales
            .AsNoTracking()
            .Where(e => e.EmpresaId == empresaId)
            .OrderBy(e => e.RazonSocial)
            .ToListAsync(ct);
        return lista.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EmpresaComercialDto>> BuscarAsync(int empresaId, string termino, CancellationToken ct = default)
    {
        var t = termino.Trim();
        var lista = await _context.EmpresasComerciales
            .AsNoTracking()
            .Where(e => e.EmpresaId == empresaId && (
                e.RazonSocial.Contains(t) ||
                (e.NombreComercial != null && e.NombreComercial.Contains(t)) ||
                (e.RFC != null && e.RFC.Contains(t)) ||
                (e.Email != null && e.Email.Contains(t))))
            .OrderBy(e => e.RazonSocial)
            .Take(50)
            .ToListAsync(ct);
        return lista.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<EmpresaComercialDto>> ObtenerPorIdAsync(int empresaComercialId, CancellationToken ct = default)
    {
        var e = await _context.EmpresasComerciales.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == empresaComercialId, ct);
        return e is null
            ? ServiceResult<EmpresaComercialDto>.Fail("Empresa comercial no encontrada.", ErrorCode.NotFound)
            : ServiceResult<EmpresaComercialDto>.Ok(MapToDto(e));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<EmpresaComercialDto>> CrearAsync(CrearEmpresaComercialDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Directorio.Editar, ct))
            return ServiceResult<EmpresaComercialDto>.Fail("Sin permiso para crear empresas (directorio.editar).", ErrorCode.Unauthorized);

        var empresa = new EmpresaComercial
        {
            EmpresaId          = dto.EmpresaId,
            RazonSocial        = dto.RazonSocial.Trim(),
            NombreComercial    = dto.NombreComercial?.Trim(),
            RFC                = dto.RFC?.Trim(),
            Email              = dto.Email?.Trim(),
            Telefono           = dto.Telefono?.Trim(),
            Direccion          = dto.Direccion?.Trim(),
            Notas              = dto.Notas?.Trim(),
            Activo             = true,
            FechaCreacion      = DateTime.UtcNow,
            UsuarioCreacionId  = usuarioId,
            Borrado            = false,
        };
        _context.EmpresasComerciales.Add(empresa);
        await _context.SaveChangesAsync(ct);
        return ServiceResult<EmpresaComercialDto>.Ok(MapToDto(empresa));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<EmpresaComercialDto>> ActualizarAsync(int empresaComercialId, ActualizarEmpresaComercialDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Directorio.Editar, ct))
            return ServiceResult<EmpresaComercialDto>.Fail("Sin permiso para editar empresas (directorio.editar).", ErrorCode.Unauthorized);

        var e = await _context.EmpresasComerciales.FirstOrDefaultAsync(x => x.Id == empresaComercialId, ct);
        if (e is null) return ServiceResult<EmpresaComercialDto>.Fail("Empresa comercial no encontrada.", ErrorCode.NotFound);

        e.RazonSocial           = dto.RazonSocial.Trim();
        e.NombreComercial       = dto.NombreComercial?.Trim();
        e.RFC                   = dto.RFC?.Trim();
        e.Email                 = dto.Email?.Trim();
        e.Telefono              = dto.Telefono?.Trim();
        e.Direccion             = dto.Direccion?.Trim();
        e.Notas                 = dto.Notas?.Trim();
        e.Activo                = dto.Activo;
        e.FechaModificacion     = DateTime.UtcNow;
        e.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<EmpresaComercialDto>.Ok(MapToDto(e));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarAsync(int empresaComercialId, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Directorio.Editar, ct))
            return ServiceResult.Fail("Sin permiso para eliminar empresas (directorio.editar).", ErrorCode.Unauthorized);

        var e = await _context.EmpresasComerciales.FirstOrDefaultAsync(x => x.Id == empresaComercialId, ct);
        if (e is null) return ServiceResult.Fail("Empresa comercial no encontrada.", ErrorCode.NotFound);

        e.Borrado               = true;
        e.Activo                = false;
        e.FechaModificacion     = DateTime.UtcNow;
        e.UsuarioModificacionId = usuarioId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static EmpresaComercialDto MapToDto(EmpresaComercial e) =>
        new(e.Id, e.EmpresaId, e.RazonSocial, e.NombreComercial,
            e.RFC, e.Email, e.Telefono, e.Direccion, e.Notas, e.Activo);
}

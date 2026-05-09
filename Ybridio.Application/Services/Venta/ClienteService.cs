using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Ventas;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Domain.Catalogos;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Venta;

/// <summary>
/// Servicio de gestión de clientes con enforcement de autorización runtime.
/// </summary>
public sealed class ClienteService : IClienteService
{
    private readonly ErpDbContext             _context;
    private readonly IErpAuthorizationService _auth;

    public ClienteService(ErpDbContext context, IErpAuthorizationService auth)
    {
        _context = context;
        _auth    = auth;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ClienteDto>> ListarPorEmpresaAsync(int empresaId, CancellationToken ct = default)
    {
        var lista = await _context.Clientes
            .AsNoTracking()
            .Where(c => c.EmpresaId == empresaId)
            .OrderBy(c => c.Nombre)
            .ToListAsync(ct);
        return lista.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ClienteDto>> BuscarAsync(int empresaId, string termino, CancellationToken ct = default)
    {
        var t = termino.Trim();
        var lista = await _context.Clientes
            .AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && (
                c.Nombre.Contains(t) ||
                (c.RFC != null && c.RFC.Contains(t)) ||
                (c.Email != null && c.Email.Contains(t))))
            .OrderBy(c => c.Nombre)
            .Take(50)
            .ToListAsync(ct);
        return lista.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<ClienteDto>> ObtenerPorIdAsync(int clienteId, CancellationToken ct = default)
    {
        var c = await _context.Clientes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == clienteId, ct);
        return c is null
            ? ServiceResult<ClienteDto>.Fail("Cliente no encontrado.", ErrorCode.NotFound)
            : ServiceResult<ClienteDto>.Ok(MapToDto(c));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<ClienteDto>> CrearAsync(CrearClienteDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Cliente.Crear, ct))
            return ServiceResult<ClienteDto>.Fail("Sin permiso para crear clientes (cliente.crear).", ErrorCode.Unauthorized);

        var cliente = new Cliente
        {
            EmpresaId         = dto.EmpresaId,
            Nombre            = dto.Nombre.Trim(),
            RFC               = dto.RFC?.Trim(),
            Email             = dto.Email?.Trim(),
            Telefono          = dto.Telefono?.Trim(),
            Direccion         = dto.Direccion?.Trim(),
            Notas             = dto.Notas?.Trim(),
            LimiteCredito     = dto.LimiteCredito,
            FechaCreacion     = DateTime.UtcNow,
            UsuarioCreacionId = usuarioId,
            Borrado           = false
        };
        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync(ct);
        return ServiceResult<ClienteDto>.Ok(MapToDto(cliente));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<ClienteDto>> ActualizarAsync(int clienteId, ActualizarClienteDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Cliente.Editar, ct))
            return ServiceResult<ClienteDto>.Fail("Sin permiso para editar clientes (cliente.editar).", ErrorCode.Unauthorized);

        var c = await _context.Clientes.FirstOrDefaultAsync(x => x.Id == clienteId, ct);
        if (c is null) return ServiceResult<ClienteDto>.Fail("Cliente no encontrado.", ErrorCode.NotFound);

        c.Nombre              = dto.Nombre.Trim();
        c.RFC                 = dto.RFC?.Trim();
        c.Email               = dto.Email?.Trim();
        c.Telefono            = dto.Telefono?.Trim();
        c.Direccion           = dto.Direccion?.Trim();
        c.Notas               = dto.Notas?.Trim();
        c.LimiteCredito       = dto.LimiteCredito;
        c.FechaModificacion   = DateTime.UtcNow;
        c.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return ServiceResult<ClienteDto>.Ok(MapToDto(c));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarAsync(int clienteId, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Cliente.Editar, ct))
            return ServiceResult.Fail("Sin permiso para eliminar clientes (cliente.editar).", ErrorCode.Unauthorized);

        var c = await _context.Clientes.FirstOrDefaultAsync(x => x.Id == clienteId, ct);
        if (c is null) return ServiceResult.Fail("Cliente no encontrado.", ErrorCode.NotFound);

        c.Borrado               = true;
        c.FechaModificacion     = DateTime.UtcNow;
        c.UsuarioModificacionId = usuarioId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static ClienteDto MapToDto(Cliente c) =>
        new(c.Id, c.EmpresaId, c.Nombre, c.RFC, c.Email, c.Telefono, c.Direccion, c.Notas, c.LimiteCredito);
}

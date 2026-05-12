using Microsoft.EntityFrameworkCore;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Directorio;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Domain.Catalogos;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Directorio;

/// <summary>
/// Gestión de RelacionesComerciales con enforcement de autorización runtime.
/// Invariante: exactamente uno de PersonaId o EmpresaComercialId debe ser no-nulo.
/// </summary>
public sealed class RelacionComercialService : IRelacionComercialService
{
    private readonly ErpDbContext             _context;
    private readonly IErpAuthorizationService _auth;

    public RelacionComercialService(ErpDbContext context, IErpAuthorizationService auth)
    {
        _context = context;
        _auth    = auth;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RelacionComercialDto>> ListarPorEmpresaAsync(
        int empresaId, TipoRelacionComercial? tipo = null, CancellationToken ct = default)
    {
        var query = _context.RelacionesComerciales
            .AsNoTracking()
            .Include(r => r.Persona)
            .Include(r => r.EmpresaComercial)
            .Where(r => r.EmpresaId == empresaId);

        if (tipo.HasValue)
            query = query.Where(r => r.TipoRelacion == tipo.Value || r.TipoRelacion == TipoRelacionComercial.Mixto);

        var lista = await query.ToListAsync(ct);
        return lista
            .Select(MapToDto)
            .OrderBy(r => r.NombreSocio)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RelacionComercialDto>> BuscarAsync(int empresaId, string termino, CancellationToken ct = default)
    {
        var t = termino.Trim().ToLower();
        var lista = await _context.RelacionesComerciales
            .AsNoTracking()
            .Include(r => r.Persona)
            .Include(r => r.EmpresaComercial)
            .Where(r => r.EmpresaId == empresaId && (
                (r.Persona != null && (r.Persona.Nombre.Contains(t) || (r.Persona.Apellidos != null && r.Persona.Apellidos.Contains(t)))) ||
                (r.EmpresaComercial != null && (r.EmpresaComercial.RazonSocial.Contains(t) || (r.EmpresaComercial.NombreComercial != null && r.EmpresaComercial.NombreComercial.Contains(t))))))
            .Take(50)
            .ToListAsync(ct);
        return lista.Select(MapToDto).OrderBy(r => r.NombreSocio).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RelacionComercialSelectorDto>> ListarParaSelectorAsync(
        int empresaId, string? termino = null, CancellationToken ct = default)
    {
        // NOTA ARQUITECTÓNICA: se usa proyección directa con join explícito en lugar de Include().
        // Include() con el QueryFilter global de EmpresaId puede resolver null en la navegación
        // cuando Persona/EmpresaComercial tiene EmpresaId distinto al tenant activo (datos legacy).
        // IgnoreQueryFilters() en los DbSets secundarios resuelve esto sin afectar la entidad raíz.

        var t = string.IsNullOrWhiteSpace(termino) ? null : termino.Trim().ToLower();

        var query =
            from rc in _context.RelacionesComerciales
            where rc.EmpresaId == empresaId && rc.Activo
            join p in _context.Personas.IgnoreQueryFilters()
                on rc.PersonaId equals p.Id into personaJoin
            from persona in personaJoin.DefaultIfEmpty()
            join ec in _context.EmpresasComerciales.IgnoreQueryFilters()
                on rc.EmpresaComercialId equals ec.Id into empresaJoin
            from empresa in empresaJoin.DefaultIfEmpty()
            select new
            {
                rc.Id,
                rc.EmpresaId,
                rc.TipoRelacion,
                rc.LimiteCredito,
                // Persona
                PersonaNombre    = persona != null ? persona.Nombre      : null,
                PersonaApellidos = persona != null ? persona.Apellidos   : null,
                PersonaRFC       = persona != null ? persona.RFC         : null,
                PersonaEmail     = persona != null ? persona.Email       : null,
                PersonaTelefono  = persona != null ? persona.Telefono    : null,
                // EmpresaComercial
                EmpresaRazonSocial   = empresa != null ? empresa.RazonSocial     : null,
                EmpresaNombreComercial = empresa != null ? empresa.NombreComercial : null,
                EmpresaRFC           = empresa != null ? empresa.RFC             : null,
                EmpresaEmail         = empresa != null ? empresa.Email           : null,
                EmpresaTelefono      = empresa != null ? empresa.Telefono        : null,
            };

        // Filtro incremental: busca en nombre, RFC, email, teléfono
        if (t is not null)
        {
            query = query.Where(x =>
                (x.PersonaNombre    != null && x.PersonaNombre.Contains(t))    ||
                (x.PersonaApellidos != null && x.PersonaApellidos.Contains(t)) ||
                (x.PersonaRFC       != null && x.PersonaRFC.Contains(t))       ||
                (x.PersonaEmail     != null && x.PersonaEmail.Contains(t))     ||
                (x.EmpresaRazonSocial    != null && x.EmpresaRazonSocial.Contains(t))    ||
                (x.EmpresaNombreComercial != null && x.EmpresaNombreComercial.Contains(t)) ||
                (x.EmpresaRFC       != null && x.EmpresaRFC.Contains(t))       ||
                (x.EmpresaEmail     != null && x.EmpresaEmail.Contains(t))
            );
        }

        var lista = await query.Take(100).ToListAsync(ct);

        return lista
            .Select(x =>
            {
                string nombreSocio;
                string tipoSocio;
                string? rfc      = null;
                string? email    = null;
                string? telefono = null;

                if (x.PersonaNombre is not null)
                {
                    nombreSocio = string.IsNullOrWhiteSpace(x.PersonaApellidos)
                        ? x.PersonaNombre
                        : $"{x.PersonaNombre} {x.PersonaApellidos}";
                    tipoSocio = "Persona Física";
                    rfc       = x.PersonaRFC;
                    email     = x.PersonaEmail;
                    telefono  = x.PersonaTelefono;
                }
                else if (x.EmpresaRazonSocial is not null)
                {
                    nombreSocio = x.EmpresaNombreComercial ?? x.EmpresaRazonSocial;
                    tipoSocio   = "Empresa";
                    rfc         = x.EmpresaRFC;
                    email       = x.EmpresaEmail;
                    telefono    = x.EmpresaTelefono;
                }
                else
                {
                    nombreSocio = $"Relación #{x.Id}";
                    tipoSocio   = "Desconocido";
                }

                var tipoDisplay = x.TipoRelacion switch
                {
                    TipoRelacionComercial.Prospecto => "Prospecto",
                    TipoRelacionComercial.Cliente   => "Cliente",
                    TipoRelacionComercial.Proveedor => "Proveedor",
                    TipoRelacionComercial.Mixto     => "Mixto",
                    _                               => x.TipoRelacion.ToString(),
                };

                // Info secundaria: tipo + datos de contacto disponibles
                var partes = new System.Collections.Generic.List<string> { tipoSocio };
                if (rfc   is { Length: > 0 }) partes.Add(rfc);
                if (email is { Length: > 0 }) partes.Add(email);
                if (telefono is { Length: > 0 }) partes.Add(telefono);

                return new RelacionComercialSelectorDto
                {
                    Id                  = x.Id,
                    NombreSocio         = nombreSocio,
                    TipoSocio           = tipoSocio,
                    TipoRelacionDisplay = tipoDisplay,
                    InfoSecundaria      = string.Join(" · ", partes),
                };
            })
            .OrderBy(r => r.NombreSocio)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<RelacionComercialDto>> ObtenerPorIdAsync(int relacionId, CancellationToken ct = default)
    {
        var r = await _context.RelacionesComerciales
            .AsNoTracking()
            .Include(x => x.Persona)
            .Include(x => x.EmpresaComercial)
            .FirstOrDefaultAsync(x => x.Id == relacionId, ct);
        return r is null
            ? ServiceResult<RelacionComercialDto>.Fail("Relación comercial no encontrada.", ErrorCode.NotFound)
            : ServiceResult<RelacionComercialDto>.Ok(MapToDto(r));
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<RelacionComercialDto>> CrearParaPersonaAsync(
        CrearRelacionComercialPersonaDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Directorio.Editar, ct))
            return ServiceResult<RelacionComercialDto>.Fail("Sin permiso (directorio.editar).", ErrorCode.Unauthorized);

        var relacion = new RelacionComercial
        {
            EmpresaId          = dto.EmpresaId,
            PersonaId          = dto.PersonaId,
            EmpresaComercialId = null,
            TipoRelacion       = dto.TipoRelacion,
            LimiteCredito      = dto.LimiteCredito,
            Observaciones      = dto.Observaciones?.Trim(),
            Activo             = true,
            FechaCreacion      = DateTime.UtcNow,
            UsuarioCreacionId  = usuarioId,
            Borrado            = false,
        };
        _context.RelacionesComerciales.Add(relacion);
        await _context.SaveChangesAsync(ct);

        return await ObtenerPorIdAsync(relacion.Id, ct);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<RelacionComercialDto>> CrearParaEmpresaAsync(
        CrearRelacionComercialEmpresaDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Directorio.Editar, ct))
            return ServiceResult<RelacionComercialDto>.Fail("Sin permiso (directorio.editar).", ErrorCode.Unauthorized);

        var relacion = new RelacionComercial
        {
            EmpresaId          = dto.EmpresaId,
            PersonaId          = null,
            EmpresaComercialId = dto.EmpresaComercialId,
            TipoRelacion       = dto.TipoRelacion,
            LimiteCredito      = dto.LimiteCredito,
            Observaciones      = dto.Observaciones?.Trim(),
            Activo             = true,
            FechaCreacion      = DateTime.UtcNow,
            UsuarioCreacionId  = usuarioId,
            Borrado            = false,
        };
        _context.RelacionesComerciales.Add(relacion);
        await _context.SaveChangesAsync(ct);

        return await ObtenerPorIdAsync(relacion.Id, ct);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<RelacionComercialDto>> ActualizarAsync(
        int relacionId, ActualizarRelacionComercialDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Directorio.Editar, ct))
            return ServiceResult<RelacionComercialDto>.Fail("Sin permiso (directorio.editar).", ErrorCode.Unauthorized);

        var r = await _context.RelacionesComerciales.FirstOrDefaultAsync(x => x.Id == relacionId, ct);
        if (r is null) return ServiceResult<RelacionComercialDto>.Fail("Relación comercial no encontrada.", ErrorCode.NotFound);

        r.TipoRelacion          = dto.TipoRelacion;
        r.LimiteCredito         = dto.LimiteCredito;
        r.Activo                = dto.Activo;
        r.Observaciones         = dto.Observaciones?.Trim();
        r.FechaModificacion     = DateTime.UtcNow;
        r.UsuarioModificacionId = usuarioId;

        await _context.SaveChangesAsync(ct);
        return await ObtenerPorIdAsync(relacionId, ct);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult> EliminarAsync(int relacionId, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Directorio.Editar, ct))
            return ServiceResult.Fail("Sin permiso (directorio.editar).", ErrorCode.Unauthorized);

        var r = await _context.RelacionesComerciales.FirstOrDefaultAsync(x => x.Id == relacionId, ct);
        if (r is null) return ServiceResult.Fail("Relación comercial no encontrada.", ErrorCode.NotFound);

        r.Borrado               = true;
        r.Activo                = false;
        r.FechaModificacion     = DateTime.UtcNow;
        r.UsuarioModificacionId = usuarioId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static RelacionComercialDto MapToDto(RelacionComercial r)
    {
        string nombreSocio;
        string tipoSocio;

        if (r.Persona is not null)
        {
            nombreSocio = r.Persona.NombreCompleto;
            tipoSocio   = "Persona Física";
        }
        else if (r.EmpresaComercial is not null)
        {
            nombreSocio = r.EmpresaComercial.NombreComercial ?? r.EmpresaComercial.RazonSocial;
            tipoSocio   = "Empresa";
        }
        else
        {
            nombreSocio = $"Relación #{r.Id}";
            tipoSocio   = "Desconocido";
        }

        var tipoDisplay = r.TipoRelacion switch
        {
            TipoRelacionComercial.Prospecto => "Prospecto",
            TipoRelacionComercial.Cliente   => "Cliente",
            TipoRelacionComercial.Proveedor => "Proveedor",
            TipoRelacionComercial.Mixto     => "Mixto",
            _                               => r.TipoRelacion.ToString(),
        };

        return new RelacionComercialDto(
            r.Id, r.EmpresaId, r.PersonaId, r.EmpresaComercialId,
            r.TipoRelacion, tipoDisplay, r.LimiteCredito, r.Activo,
            r.Observaciones, nombreSocio, tipoSocio);
    }

    /// <inheritdoc/>
    public async Task<ServiceResult<int>> GetOrCreateAsync(
        int empresaId, DirectorioSelectorDto entidad, Guid usuarioId, CancellationToken ct = default)
    {
        // Buscar relación existente para esta entidad del directorio
        RelacionComercial? existente = null;

        if (entidad.PersonaId.HasValue)
        {
            existente = await _context.RelacionesComerciales
                .FirstOrDefaultAsync(
                    r => r.EmpresaId == empresaId && r.PersonaId == entidad.PersonaId.Value,
                    ct);
        }
        else if (entidad.EmpresaComercialId.HasValue)
        {
            existente = await _context.RelacionesComerciales
                .FirstOrDefaultAsync(
                    r => r.EmpresaId == empresaId && r.EmpresaComercialId == entidad.EmpresaComercialId.Value,
                    ct);
        }
        else
        {
            return ServiceResult<int>.Fail("La entidad de directorio no tiene PersonaId ni EmpresaComercialId.", ErrorCode.ValidationFailed);
        }

        if (existente is not null)
        {
            // Reactivar si estaba inactiva (pero no eliminada con soft-delete)
            if (!existente.Activo)
            {
                existente.Activo               = true;
                existente.FechaModificacion    = DateTime.UtcNow;
                existente.UsuarioModificacionId = usuarioId;
                await _context.SaveChangesAsync(ct);
            }
            return ServiceResult<int>.Ok(existente.Id);
        }

        // Crear nueva relación — inicia como Cliente (valor operativo más común)
        var nueva = new RelacionComercial
        {
            EmpresaId          = empresaId,
            PersonaId          = entidad.PersonaId,
            EmpresaComercialId = entidad.EmpresaComercialId,
            TipoRelacion       = TipoRelacionComercial.Cliente,
            LimiteCredito      = 0,
            Activo             = true,
            FechaCreacion      = DateTime.UtcNow,
            UsuarioCreacionId  = usuarioId,
        };

        _context.RelacionesComerciales.Add(nueva);
        await _context.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(nueva.Id);
    }
}

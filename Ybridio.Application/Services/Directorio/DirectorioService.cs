using Microsoft.EntityFrameworkCore;
using Ybridio.Application.DTOs.Directorio;
using Ybridio.Infrastructure.Persistence;

namespace Ybridio.Application.Services.Directorio;

/// <summary>
/// Búsqueda en el Directorio comercial (Persona + EmpresaComercial) para el selector institucional.
/// ADR-038: el Directorio es el source of truth; RelacionComercial NO es catálogo maestro.
/// </summary>
/// <remarks>
/// Usa IgnoreQueryFilters() para evitar que el QueryFilter global de EmpresaId
/// excluya entidades cuyo EmpresaId difiere por datos legacy o discrepancias de migración.
/// El filtro de empresa se aplica explícitamente en el WHERE del query.
/// </remarks>
public sealed class DirectorioService : IDirectorioService
{
    private readonly ErpDbContext _context;

    public DirectorioService(ErpDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DirectorioSelectorDto>> BuscarParaSelectorAsync(
        int empresaId, string termino, CancellationToken ct = default)
    {
        var t = termino.Trim().ToLower();
        if (string.IsNullOrEmpty(t))
            return [];

        var personas = await _context.Personas
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && !p.Borrado && p.Activo && (
                p.Nombre.Contains(t)                                               ||
                (p.Apellidos  != null && p.Apellidos.Contains(t))                 ||
                (p.RFC        != null && p.RFC.Contains(t))                       ||
                (p.Email      != null && p.Email.Contains(t))                     ||
                (p.Telefono   != null && p.Telefono.Contains(t))))
            .Take(50)
            .Select(p => new DirectorioSelectorDto
            {
                EntityType         = DirectorioEntityType.Persona,
                PersonaId          = p.Id,
                EmpresaComercialId = null,
                DisplayName        = p.Apellidos != null && p.Apellidos.Length > 0
                    ? p.Nombre + " " + p.Apellidos
                    : p.Nombre,
                RFC      = p.RFC,
                Email    = p.Email,
                Telefono = p.Telefono,
            })
            .ToListAsync(ct);

        var empresas = await _context.EmpresasComerciales
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.EmpresaId == empresaId && !e.Borrado && e.Activo && (
                e.RazonSocial.Contains(t)                                                    ||
                (e.NombreComercial != null && e.NombreComercial.Contains(t))                 ||
                (e.RFC             != null && e.RFC.Contains(t))                             ||
                (e.Email           != null && e.Email.Contains(t))                           ||
                (e.Telefono        != null && e.Telefono.Contains(t))))
            .Take(50)
            .Select(e => new DirectorioSelectorDto
            {
                EntityType         = DirectorioEntityType.Empresa,
                PersonaId          = null,
                EmpresaComercialId = e.Id,
                DisplayName        = e.NombreComercial != null && e.NombreComercial.Length > 0
                    ? e.NombreComercial
                    : e.RazonSocial,
                RFC      = e.RFC,
                Email    = e.Email,
                Telefono = e.Telefono,
            })
            .ToListAsync(ct);

        return personas
            .Concat(empresas)
            .OrderBy(x => x.DisplayName)
            .Take(100)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<DirectorioSelectorDto?> ObtenerDtoParaSelectorAsync(
        int relacionComercialId, CancellationToken ct = default)
    {
        var relacion = await _context.RelacionesComerciales
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(r => r.Persona)
            .Include(r => r.EmpresaComercial)
            .FirstOrDefaultAsync(r => r.Id == relacionComercialId, ct);

        if (relacion is null)
            return null;

        // Resolver correctamente el tipo de entidad y mapear con datos completos.
        // REGLA: PersonaId tiene precedencia; si ambos son null la relación está corrupta.

        if (relacion.Persona is not null)
        {
            var p = relacion.Persona;
            return new DirectorioSelectorDto
            {
                EntityType         = DirectorioEntityType.Persona,
                PersonaId          = p.Id,
                EmpresaComercialId = null,
                DisplayName        = p.Apellidos is { Length: > 0 }
                    ? $"{p.Nombre} {p.Apellidos}"
                    : p.Nombre,
                RFC      = p.RFC,
                Email    = p.Email,
                Telefono = p.Telefono,
            };
        }

        if (relacion.EmpresaComercial is not null)
        {
            var e = relacion.EmpresaComercial;
            return new DirectorioSelectorDto
            {
                EntityType         = DirectorioEntityType.Empresa,
                PersonaId          = null,
                EmpresaComercialId = e.Id,
                DisplayName        = e.NombreComercial is { Length: > 0 }
                    ? e.NombreComercial
                    : e.RazonSocial,
                RFC      = e.RFC,
                Email    = e.Email,
                Telefono = e.Telefono,
            };
        }

        return null; // Relación sin entidad vinculada (datos inconsistentes)
    }
}

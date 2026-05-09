
// ── Ybridio.Application/Services/Producto/ProductoService.cs ─────────────────
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Catalogos;
using Ybridio.Application.Services.Autorizacion;
using Ybridio.Domain.Catalogos;
using Ybridio.Infrastructure.Persistence;
using DomainProducto = Ybridio.Domain.Catalogos.Producto;

namespace Ybridio.Application.Services.Producto;

public sealed class ProductoService : IProductoService
{
    private readonly ErpDbContext             _context;
    private readonly ILogger<ProductoService> _logger;
    private readonly IErpAuthorizationService _auth;

    public ProductoService(
        ErpDbContext             context,
        ILogger<ProductoService> logger,
        IErpAuthorizationService auth)
    {
        _context = context;
        _logger  = logger;
        _auth    = auth;
    }

    // ── Consultas ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ProductoDto>> ListarPorEmpresaAsync(
        int empresaId, bool soloActivos = false, CancellationToken ct = default)
    {
        IQueryable<DomainProducto> query = _context.Productos
            .AsNoTracking()
            .Where(p => p.EmpresaId == empresaId);

        if (soloActivos)
            query = query.Where(p => p.Activo);

        // Incluir solo la categoría principal para mostrar en la lista.
        // Se materializa (ToListAsync) antes de MapToDto porque el método
        // accede a navegaciones que EF Core no puede traducir a SQL directamente.
        var lista = await query
            .Include(p => p.TipoImpuesto)
            .Include(p => p.Categorias)
                .ThenInclude(pc => pc.Categoria)
            .Include(p => p.TipoProducto)
            .Include(p => p.UnidadMedida)
            .Include(p => p.Proveedor)
            .OrderBy(p => p.Nombre)
            .ToListAsync(ct);

        return lista.Select(MapToDto).ToList();
    }

    public async Task<ServiceResult<ProductoDto>> ObtenerPorIdAsync(
        int productoId, CancellationToken ct = default)
    {
        var p = await _context.Productos
            .AsNoTracking()
            .Include(p => p.TipoImpuesto)
            .Include(p => p.Categorias)
                .ThenInclude(pc => pc.Categoria)
            .Include(p => p.TipoProducto)
            .Include(p => p.UnidadMedida)
            .Include(p => p.Proveedor)
            .FirstOrDefaultAsync(p => p.Id == productoId, ct);

        if (p is null)
            return ServiceResult<ProductoDto>.Fail("Producto no encontrado.", ErrorCode.NotFound);

        return ServiceResult<ProductoDto>.Ok(MapToDto(p));
    }

    public async Task<IReadOnlyList<ProductoDto>> BuscarAsync(
        int empresaId, string termino, CancellationToken ct = default)
    {
        var t = termino.Trim();
        var resultados = await _context.Productos
            .AsNoTracking()
            .Where(p => p.EmpresaId == empresaId && p.Activo && (
                p.Nombre.Contains(t) ||
                p.Codigo.Contains(t) ||
                (p.CodigoBarras != null && p.CodigoBarras.Contains(t))))
            .Include(p => p.TipoImpuesto)
            .Include(p => p.Categorias)
                .ThenInclude(pc => pc.Categoria)
            .Include(p => p.TipoProducto)
            .Include(p => p.UnidadMedida)
            .Include(p => p.Proveedor)
            .OrderBy(p => p.Nombre)
            .Take(50)
            .ToListAsync(ct);

        return resultados.Select(MapToDto).ToList();
    }

    // ── Escritura ─────────────────────────────────────────────────────────────

    public async Task<ServiceResult<ProductoDto>> CrearAsync(
        CrearProductoDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!await _auth.PuedeAsync(PermisosClave.Producto.Crear, ct))
            return ServiceResult<ProductoDto>.Fail(
                "Sin permiso para crear productos (producto.crear).", ErrorCode.Unauthorized);

        var opId = OperationContext.CurrentId;
        _logger.LogInformation("{OperationId} Creando producto {Codigo} Empresa:{EmpresaId}",
            opId, dto.Codigo, dto.EmpresaId);

        try
        {
            // Validar código único por empresa
            var existe = await _context.Productos
                .AnyAsync(p => p.EmpresaId == dto.EmpresaId && p.Codigo == dto.Codigo, ct);

            if (existe)
                return ServiceResult<ProductoDto>.Fail(
                    $"Ya existe un producto con el código '{dto.Codigo}'.",
                    ErrorCode.ValidationFailed);

            var ahora = DateTime.UtcNow;
            var producto = new DomainProducto
            {
                EmpresaId = dto.EmpresaId,
                Codigo = dto.Codigo,
                CodigoBarras = dto.CodigoBarras,
                Nombre = dto.Nombre,
                Descripcion = dto.Descripcion,
                Precio = dto.Precio,
                PrecioMinimo = dto.PrecioMinimo,
                Costo = dto.Costo,
                IvaAplicable = dto.IvaAplicable,
                TipoImpuestoId = dto.TipoImpuestoId,
                TipoProductoId = dto.TipoProductoId,
                UnidadMedidaId = dto.UnidadMedidaId,
                StockMinimo = dto.StockMinimo,
                StockMaximo = dto.StockMaximo,
                ProveedorId = dto.ProveedorId,
                Activo = dto.Activo,
                FechaCreacion = ahora,
                UsuarioCreacionId = usuarioId,
                Borrado = false
            };

            _context.Productos.Add(producto);
            await _context.SaveChangesAsync(ct);

            // Crear relación con categoría principal (si se indicó)
            if (dto.CategoriaId.HasValue)
            {
                _context.ProductoCategorias.Add(new ProductoCategoria
                {
                    ProductoId   = producto.Id,
                    CategoriaId  = dto.CategoriaId.Value,
                    EsPrincipal  = true,
                    FechaCreacion = ahora
                });
                await _context.SaveChangesAsync(ct);
            }

            _logger.LogInformation("{OperationId} Producto {ProductoId} creado.", opId, producto.Id);

            // Recargar con navegaciones para retornar DTO completo
            return await ObtenerPorIdAsync(producto.Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{OperationId} Error al crear producto.", opId);
            return ServiceResult<ProductoDto>.Fail("Error inesperado al crear el producto.", ErrorCode.Unknown);
        }
        finally
        {
            OperationContext.Clear();
        }
    }

    public async Task<ServiceResult<ProductoDto>> ActualizarAsync(
        int productoId, ActualizarProductoDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!await _auth.PuedeAsync(PermisosClave.Producto.Editar, ct))
            return ServiceResult<ProductoDto>.Fail(
                "Sin permiso para editar productos (producto.editar).", ErrorCode.Unauthorized);

        var opId = OperationContext.CurrentId;
        try
        {
            var producto = await _context.Productos
                .FirstOrDefaultAsync(p => p.Id == productoId, ct);

            if (producto is null)
                return ServiceResult<ProductoDto>.Fail("Producto no encontrado.", ErrorCode.NotFound);

            // Validar código único si cambió
            if (producto.Codigo != dto.Codigo)
            {
                var codigoExiste = await _context.Productos
                    .AnyAsync(p => p.EmpresaId == producto.EmpresaId
                               && p.Codigo == dto.Codigo
                               && p.Id != productoId, ct);

                if (codigoExiste)
                    return ServiceResult<ProductoDto>.Fail(
                        $"Ya existe otro producto con el código '{dto.Codigo}'.",
                        ErrorCode.ValidationFailed);
            }

            var ahora = DateTime.UtcNow;
            producto.Codigo = dto.Codigo;
            producto.CodigoBarras = dto.CodigoBarras;
            producto.Nombre = dto.Nombre;
            producto.Descripcion = dto.Descripcion;
            producto.Precio = dto.Precio;
            producto.PrecioMinimo = dto.PrecioMinimo;
            producto.Costo = dto.Costo;
            producto.IvaAplicable = dto.IvaAplicable;
            producto.TipoImpuestoId = dto.TipoImpuestoId;
            producto.TipoProductoId = dto.TipoProductoId;
            producto.UnidadMedidaId = dto.UnidadMedidaId;
            producto.StockMinimo = dto.StockMinimo;
            producto.StockMaximo = dto.StockMaximo;
            producto.ProveedorId = dto.ProveedorId;
            producto.Activo = dto.Activo;
            producto.FechaModificacion = ahora;
            producto.UsuarioModificacionId = usuarioId;

            // Gestionar categoría principal en la tabla de unión N:N
            var principalExistente = await _context.ProductoCategorias
                .FirstOrDefaultAsync(pc => pc.ProductoId == productoId && pc.EsPrincipal, ct);

            if (dto.CategoriaId.HasValue)
            {
                if (principalExistente is null)
                    _context.ProductoCategorias.Add(new ProductoCategoria
                    {
                        ProductoId   = productoId,
                        CategoriaId  = dto.CategoriaId.Value,
                        EsPrincipal  = true,
                        FechaCreacion = ahora
                    });
                else if (principalExistente.CategoriaId != dto.CategoriaId.Value)
                    principalExistente.CategoriaId = dto.CategoriaId.Value;
            }
            else if (principalExistente is not null)
            {
                _context.ProductoCategorias.Remove(principalExistente);
            }

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("{OperationId} Producto {ProductoId} actualizado.", opId, productoId);
            return await ObtenerPorIdAsync(productoId, ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "{OperationId} Conflicto de concurrencia Producto:{ProductoId}.", opId, productoId);
            return ServiceResult<ProductoDto>.Fail(
                "El producto fue modificado por otro usuario. Recarga e intenta de nuevo.",
                ErrorCode.ConcurrencyConflict);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{OperationId} Error al actualizar producto {ProductoId}.", opId, productoId);
            return ServiceResult<ProductoDto>.Fail("Error inesperado al actualizar el producto.", ErrorCode.Unknown);
        }
        finally
        {
            OperationContext.Clear();
        }
    }

    public async Task<ServiceResult<ProductoDto>> ClonarAsync(
        ClonarProductoDto dto, Guid usuarioId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (!await _auth.PuedeAsync(PermisosClave.Producto.Crear, ct))
            return ServiceResult<ProductoDto>.Fail(
                "Sin permiso para clonar productos (producto.crear).", ErrorCode.Unauthorized);

        var opId = OperationContext.CurrentId;
        try
        {
            var origen = await _context.Productos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == dto.ProductoOrigenId, ct);

            if (origen is null)
                return ServiceResult<ProductoDto>.Fail("Producto origen no encontrado.", ErrorCode.NotFound);

            // Validar que el nuevo código no exista
            var codigoExiste = await _context.Productos
                .AnyAsync(p => p.EmpresaId == origen.EmpresaId && p.Codigo == dto.NuevoCodigo, ct);

            if (codigoExiste)
                return ServiceResult<ProductoDto>.Fail(
                    $"Ya existe un producto con el código '{dto.NuevoCodigo}'.",
                    ErrorCode.ValidationFailed);

            var ahora = DateTime.UtcNow;
            var clon = new DomainProducto
            {
                // Campos que cambian
                Codigo = dto.NuevoCodigo,
                Nombre = dto.NuevoNombre,
                Precio = dto.NuevoPrecio ?? origen.Precio,

                // Campos copiados del original
                EmpresaId = origen.EmpresaId,
                CodigoBarras = null,               // el clon no hereda código de barras
                Descripcion = origen.Descripcion,
                PrecioMinimo = origen.PrecioMinimo,
                Costo = origen.Costo,
                IvaAplicable = origen.IvaAplicable,
                TipoImpuestoId = origen.TipoImpuestoId,
                TipoProductoId = origen.TipoProductoId,
                UnidadMedidaId = origen.UnidadMedidaId,
                StockMinimo = origen.StockMinimo,
                StockMaximo = origen.StockMaximo,
                ProveedorId = origen.ProveedorId,
                Activo = true,

                // Auditoría
                FechaCreacion = ahora,
                UsuarioCreacionId = usuarioId,
                Borrado = false
            };

            _context.Productos.Add(clon);
            await _context.SaveChangesAsync(ct);

            // Clonar categoría principal del origen (si existe)
            var origenPrincipal = await _context.ProductoCategorias
                .AsNoTracking()
                .FirstOrDefaultAsync(pc => pc.ProductoId == dto.ProductoOrigenId && pc.EsPrincipal, ct);

            if (origenPrincipal is not null)
            {
                _context.ProductoCategorias.Add(new ProductoCategoria
                {
                    ProductoId   = clon.Id,
                    CategoriaId  = origenPrincipal.CategoriaId,
                    EsPrincipal  = true,
                    FechaCreacion = ahora
                });
                await _context.SaveChangesAsync(ct);
            }

            _logger.LogInformation("{OperationId} Producto {OrigenId} clonado a {ClonId}.",
                opId, dto.ProductoOrigenId, clon.Id);

            return await ObtenerPorIdAsync(clon.Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{OperationId} Error al clonar producto {ProductoId}.", opId, dto.ProductoOrigenId);
            return ServiceResult<ProductoDto>.Fail("Error inesperado al clonar el producto.", ErrorCode.Unknown);
        }
        finally
        {
            OperationContext.Clear();
        }
    }

    public async Task<ServiceResult> CambiarActivoAsync(
        int productoId, bool activo, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Producto.Editar, ct))
            return ServiceResult.Fail(
                "Sin permiso para editar productos (producto.editar).", ErrorCode.Unauthorized);

        try
        {
            var producto = await _context.Productos
                .FirstOrDefaultAsync(p => p.Id == productoId, ct);

            if (producto is null)
                return ServiceResult.Fail("Producto no encontrado.", ErrorCode.NotFound);

            producto.Activo = activo;
            producto.FechaModificacion = DateTime.UtcNow;
            producto.UsuarioModificacionId = usuarioId;

            await _context.SaveChangesAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar activo de producto {ProductoId}.", productoId);
            return ServiceResult.Fail("Error inesperado.", ErrorCode.Unknown);
        }
    }

    public async Task<ServiceResult> EliminarAsync(
        int productoId, Guid usuarioId, CancellationToken ct = default)
    {
        if (!await _auth.PuedeAsync(PermisosClave.Producto.Eliminar, ct))
            return ServiceResult.Fail(
                "Sin permiso para eliminar productos (producto.eliminar).", ErrorCode.Unauthorized);

        try
        {
            var producto = await _context.Productos
                .FirstOrDefaultAsync(p => p.Id == productoId, ct);

            if (producto is null)
                return ServiceResult.Fail("Producto no encontrado.", ErrorCode.NotFound);

            producto.Borrado = true;
            producto.FechaModificacion = DateTime.UtcNow;
            producto.UsuarioModificacionId = usuarioId;

            await _context.SaveChangesAsync(ct);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar producto {ProductoId}.", productoId);
            return ServiceResult.Fail("Error inesperado.", ErrorCode.Unknown);
        }
    }

    // ── Catálogos dependientes ────────────────────────────────────────────────

    public async Task<IReadOnlyList<UnidadMedidaDto>> ListarUnidadesMedidaAsync(
        int empresaId, CancellationToken ct = default) =>
        await _context.UnidadesMedida
            .AsNoTracking()
            .Where(u => u.EmpresaId == empresaId && u.Activo)
            .OrderBy(u => u.Nombre)
            .Select(u => new UnidadMedidaDto(u.Id, u.EmpresaId, u.Nombre, u.Abreviatura, u.Activo))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CategoriaProductoDto>> ListarCategoriasAsync(
        int empresaId, CancellationToken ct = default) =>
        await _context.CategoriasProducto
            .AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Activo)
            .OrderBy(c => c.Nombre)
            .Select(c => new CategoriaProductoDto(c.Id, c.EmpresaId, c.Nombre, c.Descripcion, c.Activo))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CategoriaConConteoDto>> ListarCategoriasConConteoAsync(
        int empresaId, CancellationToken ct = default) =>
        /*
         * Una sola consulta con subconsulta correlacionada — EF Core la traduce a:
         *   SELECT c.Id, c.Nombre,
         *          (SELECT COUNT(*) FROM catalogos.Producto p
         *           WHERE p.CategoriaId = c.Id AND p.Borrado = 0) AS TotalProductos
         *   FROM catalogos.CategoriaProducto c
         *   WHERE c.EmpresaId = @p AND c.Activo = 1 AND c.Borrado = 0
         *   ORDER BY c.Nombre
         *
         * Los filtros Borrado=0 los aplica el global query filter del DbContext.
         */
        await _context.CategoriasProducto
            .AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.Activo)
            .OrderBy(c => c.Nombre)
            .Select(c => new CategoriaConConteoDto(
                c.Id,
                c.Nombre,
                c.CategoriaPadreId,
                // COUNT DISTINCT implícito: contamos Productos (cada producto una sola vez),
                // aunque tenga múltiples registros en ProductoCategoria para la misma categoría.
                // El global soft-delete filter de _context.Productos excluye los borrados.
                _context.Productos.Count(p => p.Categorias.Any(pc => pc.CategoriaId == c.Id))))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TipoProductoDto>> ListarTiposProductoAsync(
        int empresaId, CancellationToken ct = default) =>
        await _context.TiposProducto
            .AsNoTracking()
            .Where(t => t.EmpresaId == empresaId && t.Activo)
            .OrderBy(t => t.Nombre)
            .Select(t => new TipoProductoDto(t.Id, t.EmpresaId, t.Nombre, t.Descripcion, t.Activo))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TipoImpuestoDto>> ListarTiposImpuestoAsync(
        int empresaId, CancellationToken ct = default) =>
        await _context.TiposImpuesto
            .AsNoTracking()
            .Where(t => t.EmpresaId == empresaId && t.Activo)
            .OrderBy(t => t.Nombre)
            .Select(t => new TipoImpuestoDto(t.Id, t.EmpresaId, t.Nombre, t.Porcentaje, t.Activo))
            .ToListAsync(ct);

    // ── Mapeo interno ─────────────────────────────────────────────────────────

    public async Task<ServiceResult> ReemplazarCategoriasAsync(
        int productoId, IReadOnlyList<int> categoriaIds, Guid usuarioId, CancellationToken ct = default)
    {
        try
        {
            var existentes = await _context.ProductoCategorias
                .Where(pc => pc.ProductoId == productoId)
                .ToListAsync(ct);

            _context.ProductoCategorias.RemoveRange(existentes);

            var ahora = DateTime.UtcNow;
            for (int i = 0; i < categoriaIds.Count; i++)
            {
                _context.ProductoCategorias.Add(new ProductoCategoria
                {
                    ProductoId    = productoId,
                    CategoriaId   = categoriaIds[i],
                    EsPrincipal   = i == 0,
                    FechaCreacion = ahora
                });
            }

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Categorías del producto {ProductoId} actualizadas: {Count}.", productoId, categoriaIds.Count);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al reemplazar categorías del producto {ProductoId}.", productoId);
            return ServiceResult.Fail("Error al actualizar categorías.", ErrorCode.Unknown);
        }
    }

    private static ProductoDto MapToDto(DomainProducto p)
    {
        var categorias  = p.Categorias ?? [];
        var principal   = categorias.FirstOrDefault(pc => pc.EsPrincipal);
        // Todos los IDs de categoría para filtrado N:N en el ViewModel (sin duplicados)
        var categoriaIds = (IReadOnlyList<int>)categorias.Select(pc => pc.CategoriaId).Distinct().ToList();

        return new ProductoDto(
            p.Id,
            p.EmpresaId,
            p.Codigo,
            p.CodigoBarras,
            p.Nombre,
            p.Descripcion,
            p.Precio,
            p.PrecioMinimo,
            p.Costo,
            p.IvaAplicable,
            p.TipoImpuestoId,
            p.TipoImpuesto?.Nombre,
            p.TipoImpuesto?.Porcentaje,
            principal?.CategoriaId,
            principal?.Categoria?.Nombre,
            p.TipoProductoId,
            p.TipoProducto?.Nombre,
            p.UnidadMedidaId,
            p.UnidadMedida?.Nombre,
            p.UnidadMedida?.Abreviatura,
            p.StockMinimo,
            p.StockMaximo,
            p.ProveedorId,
            p.Proveedor?.Nombre,
            p.Activo,
            categoriaIds);
    }
}

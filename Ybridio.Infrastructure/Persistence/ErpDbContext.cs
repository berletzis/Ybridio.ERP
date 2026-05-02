using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ybridio.Domain.Catalogos;
using Ybridio.Domain.Common;
using Ybridio.Domain.Compras;
using Ybridio.Domain.Core;
using Ybridio.Domain.Finanzas;
using Ybridio.Domain.Inventario;
using Ybridio.Domain.Seguridad;
using Ybridio.Domain.Ventas;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Infrastructure.Persistence;

/// <summary>
/// DbContext principal del ERP. Extiende IdentityDbContext con ApplicationUser y ApplicationRole
/// usando la firma simplificada de tres parámetros genéricos.
/// </summary>
public class ErpDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public ErpDbContext(DbContextOptions<ErpDbContext> options) : base(options) { }

    // ── core ──────────────────────────────────────────────────────────────────
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Tienda> Tiendas => Set<Tienda>();

    // ── catalogos ─────────────────────────────────────────────────────────────
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<UnidadMedida> UnidadesMedida => Set<UnidadMedida>();
    public DbSet<CategoriaProducto> CategoriasProducto => Set<CategoriaProducto>();
    public DbSet<TipoProducto> TiposProducto => Set<TipoProducto>();
    public DbSet<TipoImpuesto> TiposImpuesto => Set<TipoImpuesto>();
    // ── compras ───────────────────────────────────────────────────────────────
    public DbSet<OrdenCompra> OrdenesCompra => Set<OrdenCompra>();
    public DbSet<OrdenCompraDetalle> OrdenesCompraDetalle => Set<OrdenCompraDetalle>();
    public DbSet<RecepcionCompra> RecepcionesCompra => Set<RecepcionCompra>();
    public DbSet<RecepcionCompraDetalle> RecepcionesCompraDetalle => Set<RecepcionCompraDetalle>();

    // ── finanzas ──────────────────────────────────────────────────────────────
    public DbSet<Caja> Cajas => Set<Caja>();
    public DbSet<AperturaCaja> AperturasCaja => Set<AperturaCaja>();
    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>();
    public DbSet<TipoMovimientoCaja> TiposMovimientoCaja => Set<TipoMovimientoCaja>();

    // ── inventario ────────────────────────────────────────────────────────────
    public DbSet<Almacen> Almacenes => Set<Almacen>();
    public DbSet<Existencia> Existencias => Set<Existencia>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();
    public DbSet<TipoMovimientoInventario> TiposMovimientoInventario => Set<TipoMovimientoInventario>();

    // ── seguridad ─────────────────────────────────────────────────────────────
    public DbSet<Modulo> Modulos => Set<Modulo>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolesPermisos => Set<RolPermiso>();
    public DbSet<UsuarioPermiso> UsuariosPermisos => Set<UsuarioPermiso>();
    public DbSet<UsuarioTienda> UsuariosTiendas => Set<UsuarioTienda>();

    // ── ventas ────────────────────────────────────────────────────────────────
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<VentaDetalle> VentasDetalle => Set<VentaDetalle>();
    public DbSet<Factura> Facturas => Set<Factura>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Aplicar todas las configuraciones del ensamblado actual automáticamente
        builder.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);

        // Mapear las tablas auxiliares de Identity que no tienen entidad propia
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UsuarioClaim", "seguridad");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UsuarioLogin", "seguridad");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UsuarioToken", "seguridad");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RolClaim", "seguridad");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UsuarioRol", "seguridad");

        // ── QueryFilters para soft delete ─────────────────────────────────────
        // Se aplican a todas las entidades que derivan de AuditableEntity o CreationAuditEntity
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                builder.Entity(entityType.ClrType)
                    .HasQueryFilter(BuildSoftDeleteFilter<AuditableEntity>(entityType.ClrType));
            }
            else if (typeof(CreationAuditEntity).IsAssignableFrom(entityType.ClrType))
            {
                builder.Entity(entityType.ClrType)
                    .HasQueryFilter(BuildSoftDeleteFilter<CreationAuditEntity>(entityType.ClrType));
            }
        }

        // QueryFilter para ApplicationUser (Borrado directo, no hereda de base)
        builder.Entity<ApplicationUser>().HasQueryFilter(u => !u.Borrado);

        // QueryFilter para ApplicationRole (Borrado directo, no hereda de base)
        builder.Entity<ApplicationRole>().HasQueryFilter(r => !r.Borrado);
    }

    /// <summary>
    /// Construye una expresión lambda <c>e => !e.Borrado</c> para el tipo concreto dado,
    /// usando el tipo base TBase que declara la propiedad Borrado.
    /// </summary>
    private static System.Linq.Expressions.LambdaExpression BuildSoftDeleteFilter<TBase>(Type entityType)
        where TBase : class
    {
        var param = System.Linq.Expressions.Expression.Parameter(entityType, "e");
        var body = System.Linq.Expressions.Expression.Not(
            System.Linq.Expressions.Expression.Property(param, nameof(AuditableEntity.Borrado)));
        return System.Linq.Expressions.Expression.Lambda(body, param);
    }
}


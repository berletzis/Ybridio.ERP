using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq.Expressions;
using System.Reflection;
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
/// DbContext principal del ERP.
/// Aplica globalmente:
///   1. Soft-delete (!Borrado) a toda entidad que herede de AuditableEntity / CreationAuditEntity.
///   2. Filtro de empresa (EmpresaId == session.EmpresaId) a toda entidad con FK EmpresaId.
///      Cuando session.EmpresaId == 0 (tooling / tests), el filtro de empresa se omite.
/// </summary>
public class ErpDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly ISessionContext _session;

    public ErpDbContext(DbContextOptions<ErpDbContext> options, ISessionContext session)
        : base(options)
    {
        _session = session;
    }

    // ── core ──────────────────────────────────────────────────────────────────
    public DbSet<Empresa>  Empresas   => Set<Empresa>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();

    // ── directorio (business partners) ────────────────────────────────────────
    public DbSet<Persona>            Personas             => Set<Persona>();
    public DbSet<EmpresaComercial>   EmpresasComerciales  => Set<EmpresaComercial>();
    public DbSet<RelacionComercial>  RelacionesComerciales => Set<RelacionComercial>();

    // ── catalogos ─────────────────────────────────────────────────────────────
    public DbSet<Cliente>           Clientes          => Set<Cliente>();
    public DbSet<Producto>          Productos         => Set<Producto>();
    public DbSet<ProductoCategoria> ProductoCategorias => Set<ProductoCategoria>();
    public DbSet<ProductoSucursal>  ProductoSucursales   => Set<ProductoSucursal>();
    public DbSet<Proveedor>         Proveedores       => Set<Proveedor>();
    public DbSet<UnidadMedida>      UnidadesMedida    => Set<UnidadMedida>();
    public DbSet<CategoriaProducto> CategoriasProducto => Set<CategoriaProducto>();
    public DbSet<TipoProducto>      TiposProducto     => Set<TipoProducto>();
    public DbSet<TipoImpuesto>      TiposImpuesto     => Set<TipoImpuesto>();
    public DbSet<ParametroGlobal>   ParametrosGlobal  => Set<ParametroGlobal>();
    public DbSet<OtroCargo>         OtrosCargos       => Set<OtroCargo>();
    public DbSet<SerieDocumento>    SeriesDocumento   => Set<SerieDocumento>();

    // ── compras ───────────────────────────────────────────────────────────────
    public DbSet<OrdenCompra>          OrdenesCompra          => Set<OrdenCompra>();
    public DbSet<OrdenCompraDetalle>   OrdenesCompraDetalle   => Set<OrdenCompraDetalle>();
    public DbSet<RecepcionCompra>      RecepcionesCompra      => Set<RecepcionCompra>();
    public DbSet<RecepcionCompraDetalle> RecepcionesCompraDetalle => Set<RecepcionCompraDetalle>();

    // ── finanzas ──────────────────────────────────────────────────────────────
    public DbSet<Caja>                    Cajas                     => Set<Caja>();
    public DbSet<AperturaCaja>            AperturasCaja             => Set<AperturaCaja>();
    public DbSet<MovimientoCaja>          MovimientosCaja           => Set<MovimientoCaja>();
    public DbSet<TipoMovimientoCaja>      TiposMovimientoCaja       => Set<TipoMovimientoCaja>();
    public DbSet<CategoriaFinanciera>     CategoriasFinancieras     => Set<CategoriaFinanciera>();
    public DbSet<MovimientoFinanciero>    MovimientosFinancieros    => Set<MovimientoFinanciero>();
    public DbSet<CuentaPorCobrar>         CuentasPorCobrar          => Set<CuentaPorCobrar>();
    public DbSet<CuentaPorPagar>          CuentasPorPagar           => Set<CuentaPorPagar>();

    // ── inventario ────────────────────────────────────────────────────────────
    public DbSet<Almacen>                    Almacenes                  => Set<Almacen>();
    public DbSet<Existencia>                 Existencias                => Set<Existencia>();
    public DbSet<MovimientoInventario>       MovimientosInventario      => Set<MovimientoInventario>();
    public DbSet<TipoMovimientoInventario>   TiposMovimientoInventario  => Set<TipoMovimientoInventario>();
    public DbSet<ConceptoEntrada>            ConceptosEntrada           => Set<ConceptoEntrada>();
    public DbSet<ConceptoSalida>             ConceptosSalida            => Set<ConceptoSalida>();
    public DbSet<EstatusEntrada>             EstatusEntrada             => Set<EstatusEntrada>();
    public DbSet<EstatusSalida>              EstatusSalida              => Set<EstatusSalida>();
    public DbSet<Entrada>                    Entradas                   => Set<Entrada>();
    public DbSet<EntradaDetalle>             EntradasDetalle            => Set<EntradaDetalle>();
    public DbSet<Salida>                     Salidas                    => Set<Salida>();
    public DbSet<SalidaDetalle>              SalidasDetalle             => Set<SalidaDetalle>();
    public DbSet<Traspaso>                   Traspasos                  => Set<Traspaso>();
    public DbSet<AjusteInventario>           AjustesInventario          => Set<AjusteInventario>();
    public DbSet<AjusteInventarioDetalle>    AjustesInventarioDetalle   => Set<AjusteInventarioDetalle>();

    // ── seguridad ─────────────────────────────────────────────────────────────
    public DbSet<Modulo>         Modulos             => Set<Modulo>();
    public DbSet<Permiso>        Permisos            => Set<Permiso>();
    public DbSet<RolPermiso>     RolesPermisos       => Set<RolPermiso>();
    public DbSet<UsuarioPermiso> UsuariosPermisos    => Set<UsuarioPermiso>();
    public DbSet<UsuarioSucursal> UsuariosSucursales => Set<UsuarioSucursal>();
    public DbSet<Perfil>         Perfiles            => Set<Perfil>();
    public DbSet<PerfilPermiso>  PerfilPermisos      => Set<PerfilPermiso>();
    public DbSet<UsuarioPerfil>  UsuariosPerfiles    => Set<UsuarioPerfil>();
    public DbSet<UsuarioAlmacen> UsuariosAlmacenes   => Set<UsuarioAlmacen>();

    // ── ventas ────────────────────────────────────────────────────────────────
    public DbSet<Venta>                  Ventas                   => Set<Venta>();
    public DbSet<VentaDetalle>           VentasDetalle            => Set<VentaDetalle>();
    public DbSet<PagoVenta>              PagosVenta               => Set<PagoVenta>();
    public DbSet<Factura>                Facturas                 => Set<Factura>();
    public DbSet<Cotizacion>             Cotizaciones             => Set<Cotizacion>();
    public DbSet<CotizacionDetalle>      CotizacionesDetalle      => Set<CotizacionDetalle>();
    public DbSet<CotizacionCargo>        CotizacionesCargos       => Set<CotizacionCargo>();
    public DbSet<Pedido>                 Pedidos                  => Set<Pedido>();
    public DbSet<PedidoDetalle>          PedidosDetalle           => Set<PedidoDetalle>();
    public DbSet<PedidoCargo>            PedidosCargos            => Set<PedidoCargo>();
    public DbSet<AnticipoPedido>         AnticipoPedidos          => Set<AnticipoPedido>();
    public DbSet<OrdenTrabajo>           OrdenesTrabajo           => Set<OrdenTrabajo>();
    public DbSet<OrdenTrabajoMaterial>   OrdenTrabajoMateriales   => Set<OrdenTrabajoMaterial>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);

        // Mapear tablas auxiliares de Identity
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UsuarioClaim", "seguridad");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RolClaim",     "seguridad");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UsuarioRol",    "seguridad");

        // UsuarioLogin: clave compuesta con NVARCHAR(128) para no superar el límite
        // de 900 bytes por índice agrupado de SQL Server (450*2 = 1800 > 900)
        builder.Entity<IdentityUserLogin<Guid>>()
            .ToTable("UsuarioLogin", "seguridad")
            .HasKey(e => new { e.LoginProvider, e.ProviderKey });
        builder.Entity<IdentityUserLogin<Guid>>()
            .Property(e => e.LoginProvider).HasMaxLength(128);
        builder.Entity<IdentityUserLogin<Guid>>()
            .Property(e => e.ProviderKey).HasMaxLength(128);

        // UsuarioToken: clave compuesta con NVARCHAR(128) para la misma razón
        // (16 bytes Guid + 450 + 450 = 916 > 900)
        builder.Entity<IdentityUserToken<Guid>>()
            .ToTable("UsuarioToken", "seguridad")
            .HasKey(e => new { e.UserId, e.LoginProvider, e.Name });
        builder.Entity<IdentityUserToken<Guid>>()
            .Property(e => e.LoginProvider).HasMaxLength(128);
        builder.Entity<IdentityUserToken<Guid>>()
            .Property(e => e.Name).HasMaxLength(128);

        // ── QueryFilters combinados: soft-delete + empresa ────────────────────
        //
        // Reglas:
        //  • !Borrado      → toda entidad con AuditableEntity / CreationAuditEntity base
        //  • EmpresaId     → toda entidad con propiedad EmpresaId (int), excepto Empresa misma
        //  • session.EmpresaId == 0 → omite el filtro de empresa (design-time / tests)
        //
        // HasQueryFilter solo puede llamarse UNA vez por tipo; los filtros se combinan con &&.

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var type = entityType.ClrType;
            LambdaExpression? filter = null;

            // Soft-delete
            if (typeof(AuditableEntity).IsAssignableFrom(type))
                filter = BuildSoftDeleteFilter(type);
            else if (typeof(CreationAuditEntity).IsAssignableFrom(type))
                filter = BuildSoftDeleteFilter(type);

            // Filtro de empresa (excluye Empresa misma para no bloquear la consulta raíz)
            if (type != typeof(Empresa) && HasIntProperty(type, "EmpresaId"))
            {
                var empresaFilter = BuildEmpresaFilter(type);
                filter = filter is null ? empresaFilter : CombineAnd(filter, empresaFilter);
            }

            if (filter is not null)
                builder.Entity(type).HasQueryFilter(filter);
        }

        // ApplicationUser: no hereda de base pero tiene Borrado + EmpresaId
        builder.Entity<ApplicationUser>()
            .HasQueryFilter(u => !u.Borrado &&
                (_session.EmpresaId == 0 || u.EmpresaId == _session.EmpresaId));

        // ApplicationRole: solo soft-delete (roles son globales, sin empresa)
        builder.Entity<ApplicationRole>().HasQueryFilter(r => !r.Borrado);
    }

    // ── Filter builders ───────────────────────────────────────────────────────

    private static LambdaExpression BuildSoftDeleteFilter(Type entityType)
    {
        var param = Expression.Parameter(entityType, "e");
        var body  = Expression.Not(Expression.Property(param, nameof(AuditableEntity.Borrado)));
        return Expression.Lambda(body, param);
    }

    /// <summary>
    /// Crea: e => _session.EmpresaId == 0 || e.EmpresaId == _session.EmpresaId
    /// Captura _session por REFERENCIA (no por valor) para que se evalúe en cada query.
    /// </summary>
    private LambdaExpression BuildEmpresaFilter(Type entityType)
    {
        var param = Expression.Parameter(entityType, "e");

        // Acceso en tiempo de query: _session.EmpresaId
        var sessionRef  = Expression.Constant(_session, typeof(ISessionContext));
        var sessionId   = Expression.Property(sessionRef, nameof(ISessionContext.EmpresaId));
        var zero        = Expression.Constant(0, typeof(int));
        var rowId       = Expression.Property(param, "EmpresaId");

        // _session.EmpresaId == 0 → omitir filtro (design-time / sin sesión)
        var noSession   = Expression.Equal(sessionId, zero);
        var matches     = Expression.Equal(rowId, sessionId);
        var body        = Expression.OrElse(noSession, matches);

        return Expression.Lambda(body, param);
    }

    private static LambdaExpression CombineAnd(LambdaExpression left, LambdaExpression right)
    {
        var param     = left.Parameters[0];
        var rightBody = new ParamReplacer(right.Parameters[0], param).Visit(right.Body);
        return Expression.Lambda(Expression.AndAlso(left.Body, rightBody), param);
    }

    private static bool HasIntProperty(Type type, string name)
    {
        var p = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        return p?.PropertyType == typeof(int);
    }

    private sealed class ParamReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from, _to;
        public ParamReplacer(ParameterExpression from, ParameterExpression to)
            { _from = from; _to = to; }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _from ? _to : base.VisitParameter(node);
    }
}

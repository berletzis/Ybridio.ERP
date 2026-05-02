// ── ProductoConfiguration.cs — REEMPLAZAR COMPLETO ───────────────────────────
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Catalogos;

internal sealed class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.ToTable("Producto", "catalogos");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        // Identificación
        builder.Property(e => e.Codigo).IsRequired().HasMaxLength(100);
        builder.Property(e => e.CodigoBarras).HasMaxLength(100);
        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(300);
        builder.Property(e => e.Descripcion).HasMaxLength(500);

        // Precios
        builder.Property(e => e.Precio).IsRequired().HasColumnType("decimal(18,6)");
        builder.Property(e => e.PrecioMinimo).HasColumnType("decimal(18,6)");
        builder.Property(e => e.Costo).HasColumnType("decimal(18,6)");

        // Impuesto
        builder.Property(e => e.IvaAplicable).IsRequired().HasDefaultValue(true);

        // Stock
        builder.Property(e => e.StockMinimo).HasColumnType("decimal(18,6)");
        builder.Property(e => e.StockMaximo).HasColumnType("decimal(18,6)");

        // Estado
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);

        // Auditoría
        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        // Relaciones
        builder.HasOne(e => e.Empresa).WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_Producto_Empresa");

        builder.HasOne(e => e.TipoImpuesto).WithMany()
            .HasForeignKey(e => e.TipoImpuestoId)
            .HasConstraintName("FK_Producto_TipoImpuesto")
            .IsRequired(false);

        builder.HasOne(e => e.Categoria).WithMany()
            .HasForeignKey(e => e.CategoriaId)
            .HasConstraintName("FK_Producto_CategoriaProducto")
            .IsRequired(false);

        builder.HasOne(e => e.TipoProducto).WithMany()
            .HasForeignKey(e => e.TipoProductoId)
            .HasConstraintName("FK_Producto_TipoProducto")
            .IsRequired(false);

        builder.HasOne(e => e.UnidadMedida).WithMany()
            .HasForeignKey(e => e.UnidadMedidaId)
            .HasConstraintName("FK_Producto_UnidadMedida")
            .IsRequired(false);

        builder.HasOne(e => e.Proveedor).WithMany()
            .HasForeignKey(e => e.ProveedorId)
            .HasConstraintName("FK_Producto_Proveedor")
            .IsRequired(false);

        // Índices
        builder.HasIndex(e => new { e.EmpresaId, e.Codigo })
            .IsUnique()
            .HasDatabaseName("UX_Producto_EmpresaId_Codigo");

        builder.HasIndex(e => new { e.EmpresaId, e.CodigoBarras })
            .HasDatabaseName("IX_Producto_CodigoBarras")
            .HasFilter("[CodigoBarras] IS NOT NULL");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Catalogos;

namespace Ybridio.Infrastructure.Persistence.Configurations.Catalogos;

internal sealed class ProductoCategoriaConfiguration : IEntityTypeConfiguration<ProductoCategoria>
{
    public void Configure(EntityTypeBuilder<ProductoCategoria> builder)
    {
        builder.ToTable("ProductoCategoria", "catalogos");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.EsPrincipal).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");

        builder.HasOne(e => e.Producto)
            .WithMany(p => p.Categorias)
            .HasForeignKey(e => e.ProductoId)
            .HasConstraintName("FK_ProductoCategoria_Producto")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Categoria)
            .WithMany(c => c.ProductoCategorias)
            .HasForeignKey(e => e.CategoriaId)
            .HasConstraintName("FK_ProductoCategoria_Categoria")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

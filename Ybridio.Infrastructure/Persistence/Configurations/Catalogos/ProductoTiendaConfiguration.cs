using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Catalogos;

namespace Ybridio.Infrastructure.Persistence.Configurations.Catalogos;

public sealed class ProductoSucursalConfiguration : IEntityTypeConfiguration<ProductoSucursal>
{
    public void Configure(EntityTypeBuilder<ProductoSucursal> builder)
    {
        builder.ToTable("ProductoSucursal", "core");

        builder.HasKey(pt => pt.Id);
        builder.Property(pt => pt.Id).ValueGeneratedOnAdd();

        builder.Property(pt => pt.PrecioOverride)
            .HasColumnType("decimal(18,6)");

        builder.Property(pt => pt.Activo)
            .HasDefaultValue(true);

        builder.Property(pt => pt.FechaCreacion)
            .HasDefaultValueSql("getdate()")
            .ValueGeneratedOnAdd();

        // Un producto puede estar en múltiples tiendas (no agrega colección inversa a Producto)
        builder.HasOne(pt => pt.Producto)
            .WithMany()
            .HasForeignKey(pt => pt.ProductoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Una sucursal puede tener múltiples productos (no agrega colección inversa a Sucursal)
        builder.HasOne(pt => pt.Sucursal)
            .WithMany()
            .HasForeignKey(pt => pt.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        // Un producto solo puede aparecer una vez por sucursal
        builder.HasIndex(pt => new { pt.ProductoId, pt.SucursalId })
            .IsUnique();
    }
}

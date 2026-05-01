using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Compras;

namespace Ybridio.Infrastructure.Persistence.Configurations.Compras;

internal sealed class OrdenCompraDetalleConfiguration : IEntityTypeConfiguration<OrdenCompraDetalle>
{
    public void Configure(EntityTypeBuilder<OrdenCompraDetalle> builder)
    {
        builder.ToTable("OrdenCompraDetalle", "compras");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Cantidad).IsRequired().HasColumnType("decimal(18,6)");
        builder.Property(e => e.Precio).IsRequired().HasColumnType("decimal(18,6)");
        builder.Property(e => e.Importe).IsRequired().HasColumnType("decimal(18,6)");

        builder.Property(e => e.FechaCreacion).IsRequired()
            .HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.OrdenCompra)
            .WithMany(e => e.Detalles)
            .HasForeignKey(e => e.OrdenCompraId);

        builder.HasOne(e => e.Producto)
            .WithMany()
            .HasForeignKey(e => e.ProductoId);
    }
}

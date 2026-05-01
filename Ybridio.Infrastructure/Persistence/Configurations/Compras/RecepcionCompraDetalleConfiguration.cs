using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Compras;

namespace Ybridio.Infrastructure.Persistence.Configurations.Compras;

internal sealed class RecepcionCompraDetalleConfiguration : IEntityTypeConfiguration<RecepcionCompraDetalle>
{
    public void Configure(EntityTypeBuilder<RecepcionCompraDetalle> builder)
    {
        builder.ToTable("RecepcionCompraDetalle", "compras");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Cantidad).IsRequired().HasColumnType("decimal(18,4)");

        builder.HasOne(e => e.RecepcionCompra)
            .WithMany(e => e.Detalles)
            .HasForeignKey(e => e.RecepcionCompraId);

        builder.HasOne(e => e.Producto)
            .WithMany()
            .HasForeignKey(e => e.ProductoId);
    }
}

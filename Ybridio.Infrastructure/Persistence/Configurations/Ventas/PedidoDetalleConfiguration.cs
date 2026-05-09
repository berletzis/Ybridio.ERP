using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Ventas;

namespace Ybridio.Infrastructure.Persistence.Configurations.Ventas;

public class PedidoDetalleConfiguration : IEntityTypeConfiguration<PedidoDetalle>
{
    public void Configure(EntityTypeBuilder<PedidoDetalle> builder)
    {
        builder.ToTable("PedidoDetalle", "ventas");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Descripcion).IsRequired().HasMaxLength(300);
        builder.Property(e => e.Cantidad).IsRequired().HasColumnType("decimal(18,6)");
        builder.Property(e => e.PrecioUnitario).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(e => e.Importe).IsRequired().HasColumnType("decimal(18,2)");

        builder.HasOne(e => e.Pedido).WithMany(p => p.Detalles).HasForeignKey(e => e.PedidoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Producto).WithMany().HasForeignKey(e => e.ProductoId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
    }
}

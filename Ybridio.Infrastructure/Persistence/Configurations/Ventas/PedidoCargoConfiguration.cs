using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Ventas;

namespace Ybridio.Infrastructure.Persistence.Configurations.Ventas;

public class PedidoCargoConfiguration : IEntityTypeConfiguration<PedidoCargo>
{
    public void Configure(EntityTypeBuilder<PedidoCargo> builder)
    {
        builder.ToTable("PedidoCargo", "ventas");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Descripcion).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Importe).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(e => e.AplicaIva).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.Orden).IsRequired().HasDefaultValue(0);

        builder.HasOne(e => e.Pedido)
            .WithMany(p => p.Cargos)
            .HasForeignKey(e => e.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.PedidoId, e.Orden })
            .HasDatabaseName("IX_PedidoCargo_Pedido_Orden");
    }
}

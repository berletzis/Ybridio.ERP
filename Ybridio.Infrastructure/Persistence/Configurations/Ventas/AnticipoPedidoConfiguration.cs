using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Ventas;

namespace Ybridio.Infrastructure.Persistence.Configurations.Ventas;

internal sealed class AnticipoPedidoConfiguration : IEntityTypeConfiguration<AnticipoPedido>
{
    public void Configure(EntityTypeBuilder<AnticipoPedido> builder)
    {
        builder.ToTable("AnticipoPedido", "ventas");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Fecha).IsRequired();
        builder.Property(e => e.Monto).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.FormaPago).HasMaxLength(50).IsRequired().HasDefaultValue("Efectivo");
        builder.Property(e => e.Referencia).HasMaxLength(100).IsRequired(false);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.FechaModificacion).IsRequired(false);
        builder.Property(e => e.UsuarioModificacionId).IsRequired(false);
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Pedido)
            .WithMany(p => p.Anticipos)
            .HasForeignKey(e => e.PedidoId)
            .HasConstraintName("FK_AnticipoPedido_Pedido")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.PedidoId).HasDatabaseName("IX_AnticipoPedido_PedidoId");
    }
}

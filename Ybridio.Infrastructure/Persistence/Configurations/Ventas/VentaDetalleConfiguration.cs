using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Ventas;

namespace Ybridio.Infrastructure.Persistence.Configurations.Ventas;

internal sealed class VentaDetalleConfiguration : IEntityTypeConfiguration<VentaDetalle>
{
    public void Configure(EntityTypeBuilder<VentaDetalle> builder)
    {
        builder.ToTable("VentaDetalle", "ventas");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Cantidad).IsRequired().HasColumnType("decimal(18,6)");
        builder.Property(e => e.Precio).HasColumnType("decimal(18,2)");
        builder.Property(e => e.Importe).HasColumnType("decimal(18,2)");

        builder.Property(e => e.FechaCreacion).IsRequired()
            .HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Venta)
            .WithMany(e => e.Detalles)
            .HasForeignKey(e => e.VentaId)
            .HasConstraintName("FK_VentaDetalle_Venta");

        builder.HasOne(e => e.Producto)
            .WithMany()
            .HasForeignKey(e => e.ProductoId)
            .HasConstraintName("FK_VentaDetalle_Producto");

        builder.HasOne(e => e.Almacen)
            .WithMany()
            .HasForeignKey(e => e.AlmacenId)
            .HasConstraintName("FK_VentaDetalle_Almacen")
            .IsRequired(false);
    }
}

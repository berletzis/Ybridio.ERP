using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Inventario;

namespace Ybridio.Infrastructure.Persistence.Configurations.Inventario;

internal sealed class SalidaDetalleConfiguration : IEntityTypeConfiguration<SalidaDetalle>
{
    public void Configure(EntityTypeBuilder<SalidaDetalle> builder)
    {
        builder.ToTable("SalidaDetalle", "inventario");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.NumeroLinea).IsRequired().HasDefaultValue((short)1);
        builder.Property(e => e.Cantidad).IsRequired().HasColumnType("decimal(18,6)");
        builder.Property(e => e.CostoUnitario).IsRequired().HasColumnType("decimal(18,6)").HasDefaultValue(0m);
        builder.Property(e => e.Importe).IsRequired().HasColumnType("decimal(18,6)").HasDefaultValue(0m);
        builder.Property(e => e.PrecioUnitario).HasColumnType("decimal(18,6)");
        builder.Property(e => e.Descuento).HasColumnType("decimal(18,6)");
        builder.Property(e => e.CodigoBarras).HasMaxLength(100);
        builder.Property(e => e.Sku).HasMaxLength(100);
        builder.Property(e => e.Observaciones).HasMaxLength(500);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasIndex(e => e.SalidaId).HasDatabaseName("IX_SalidaDetalle_Salida");
        builder.HasIndex(e => e.ProductoId).HasDatabaseName("IX_SalidaDetalle_Producto");

        builder.HasOne(e => e.Salida).WithMany(h => h.Detalles).HasForeignKey(e => e.SalidaId)
            .HasConstraintName("FK_SalidaDetalle_Salida");
        builder.HasOne(e => e.Producto).WithMany().HasForeignKey(e => e.ProductoId)
            .HasConstraintName("FK_SalidaDetalle_Producto").OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.EntradaDetalleOrigen).WithMany().HasForeignKey(e => e.EntradaDetalleOrigenId)
            .HasConstraintName("FK_SalidaDetalle_EntradaDetalleOrigen").OnDelete(DeleteBehavior.NoAction);
    }
}

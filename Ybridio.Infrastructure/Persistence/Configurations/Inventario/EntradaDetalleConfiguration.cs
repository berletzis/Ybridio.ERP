using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Inventario;

namespace Ybridio.Infrastructure.Persistence.Configurations.Inventario;

internal sealed class EntradaDetalleConfiguration : IEntityTypeConfiguration<EntradaDetalle>
{
    public void Configure(EntityTypeBuilder<EntradaDetalle> builder)
    {
        builder.ToTable("EntradaDetalle", "inventario");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.NumeroLinea).IsRequired().HasDefaultValue((short)1);
        builder.Property(e => e.CantidadEsperada).IsRequired().HasColumnType("decimal(18,6)").HasDefaultValue(0m);
        builder.Property(e => e.CantidadRecibida).IsRequired().HasColumnType("decimal(18,6)").HasDefaultValue(0m);
        builder.Property(e => e.CostoUnitario).IsRequired().HasColumnType("decimal(18,6)").HasDefaultValue(0m);
        builder.Property(e => e.Importe).IsRequired().HasColumnType("decimal(18,6)").HasDefaultValue(0m);
        builder.Property(e => e.CodigoBarras).HasMaxLength(100);
        builder.Property(e => e.Sku).HasMaxLength(100);
        builder.Property(e => e.Observaciones).HasMaxLength(500);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasIndex(e => e.EntradaId).HasDatabaseName("IX_EntradaDetalle_Entrada");
        builder.HasIndex(e => e.ProductoId).HasDatabaseName("IX_EntradaDetalle_Producto");

        builder.HasOne(e => e.Entrada).WithMany(h => h.Detalles).HasForeignKey(e => e.EntradaId)
            .HasConstraintName("FK_EntradaDetalle_Entrada");
        builder.HasOne(e => e.Producto).WithMany().HasForeignKey(e => e.ProductoId)
            .HasConstraintName("FK_EntradaDetalle_Producto").OnDelete(DeleteBehavior.NoAction);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Inventario;

namespace Ybridio.Infrastructure.Persistence.Configurations.Inventario;

internal sealed class AjusteInventarioDetalleConfiguration : IEntityTypeConfiguration<AjusteInventarioDetalle>
{
    public void Configure(EntityTypeBuilder<AjusteInventarioDetalle> builder)
    {
        builder.ToTable("AjusteInventarioDetalle", "inventario");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.CantidadSistema).IsRequired().HasColumnType("decimal(18,6)").HasDefaultValue(0m);
        builder.Property(e => e.CantidadFisica).IsRequired().HasColumnType("decimal(18,6)").HasDefaultValue(0m);
        // Columna calculada persistida en SQL Server: [CantidadFisica] - [CantidadSistema]
        builder.Property(e => e.Diferencia).HasComputedColumnSql("[CantidadFisica] - [CantidadSistema]", stored: true);
        builder.Property(e => e.CostoUnitario).IsRequired().HasColumnType("decimal(18,6)").HasDefaultValue(0m);
        builder.Property(e => e.Observaciones).HasMaxLength(500);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.AjusteInventario).WithMany(a => a.Detalles).HasForeignKey(e => e.AjusteInventarioId)
            .HasConstraintName("FK_AjusteDetalle_Ajuste");
        builder.HasOne(e => e.Producto).WithMany().HasForeignKey(e => e.ProductoId)
            .HasConstraintName("FK_AjusteDetalle_Producto").OnDelete(DeleteBehavior.NoAction);
    }
}

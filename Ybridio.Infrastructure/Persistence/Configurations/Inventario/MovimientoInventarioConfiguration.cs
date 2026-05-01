using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Inventario;

namespace Ybridio.Infrastructure.Persistence.Configurations.Inventario;

internal sealed class MovimientoInventarioConfiguration : IEntityTypeConfiguration<MovimientoInventario>
{
    public void Configure(EntityTypeBuilder<MovimientoInventario> builder)
    {
        builder.ToTable("MovimientoInventario", "inventario");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Cantidad).IsRequired().HasColumnType("decimal(18,6)");
        builder.Property(e => e.CostoUnitario).IsRequired().HasColumnType("decimal(18,6)");
        builder.Property(e => e.Total).IsRequired().HasColumnType("decimal(18,6)");
        builder.Property(e => e.Referencia).HasMaxLength(100);
        builder.Property(e => e.Fecha).IsRequired();

        builder.Property(e => e.FechaCreacion).IsRequired()
            .HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId);

        builder.HasOne(e => e.Almacen)
            .WithMany()
            .HasForeignKey(e => e.AlmacenId);

        builder.HasOne(e => e.Producto)
            .WithMany()
            .HasForeignKey(e => e.ProductoId);

        builder.HasOne(e => e.TipoMovimiento)
            .WithMany(e => e.Movimientos)
            .HasForeignKey(e => e.TipoMovimientoId);
    }
}

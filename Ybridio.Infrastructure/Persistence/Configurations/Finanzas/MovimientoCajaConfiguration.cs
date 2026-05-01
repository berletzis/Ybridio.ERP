using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Finanzas;

namespace Ybridio.Infrastructure.Persistence.Configurations.Finanzas;

internal sealed class MovimientoCajaConfiguration : IEntityTypeConfiguration<MovimientoCaja>
{
    public void Configure(EntityTypeBuilder<MovimientoCaja> builder)
    {
        builder.ToTable("MovimientoCaja", "finanzas");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Tipo).HasMaxLength(50);
        builder.Property(e => e.Monto).IsRequired().HasColumnType("decimal(18,6)");
        builder.Property(e => e.Referencia).HasMaxLength(100);
        builder.Property(e => e.Fecha).IsRequired();

        builder.Property(e => e.FechaCreacion).IsRequired()
            .HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Caja)
            .WithMany(e => e.Movimientos)
            .HasForeignKey(e => e.CajaId);

        builder.HasOne(e => e.TipoMovimiento)
            .WithMany(e => e.Movimientos)
            .HasForeignKey(e => e.TipoMovimientoId)
            .HasConstraintName("FK_MovCaja_Tipo")
            .IsRequired(false);
    }
}

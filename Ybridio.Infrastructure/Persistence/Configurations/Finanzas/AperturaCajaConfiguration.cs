using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Finanzas;

namespace Ybridio.Infrastructure.Persistence.Configurations.Finanzas;

internal sealed class AperturaCajaConfiguration : IEntityTypeConfiguration<AperturaCaja>
{
    public void Configure(EntityTypeBuilder<AperturaCaja> builder)
    {
        builder.ToTable("AperturaCaja", "finanzas");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.MontoInicial).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(e => e.MontoFinal).HasColumnType("decimal(18,6)");
        builder.Property(e => e.Activa).IsRequired().HasDefaultValue(true);

        builder.Property(e => e.FechaCreacion).IsRequired()
            .HasDefaultValueSql("getdate()");
        // UsuarioCreacionId en DB es nullable para esta tabla
        builder.Property(e => e.UsuarioCreacionId).IsRequired(false);
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Caja)
            .WithMany(e => e.Aperturas)
            .HasForeignKey(e => e.CajaId);
    }
}

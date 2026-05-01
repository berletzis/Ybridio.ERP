using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Finanzas;

namespace Ybridio.Infrastructure.Persistence.Configurations.Finanzas;

internal sealed class TipoMovimientoCajaConfiguration : IEntityTypeConfiguration<TipoMovimientoCaja>
{
    public void Configure(EntityTypeBuilder<TipoMovimientoCaja> builder)
    {
        builder.ToTable("TipoMovimientoCaja", "finanzas");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(50);
    }
}

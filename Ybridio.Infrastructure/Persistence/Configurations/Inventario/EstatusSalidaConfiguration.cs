using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Inventario;

namespace Ybridio.Infrastructure.Persistence.Configurations.Inventario;

internal sealed class EstatusSalidaConfiguration : IEntityTypeConfiguration<EstatusSalida>
{
    public void Configure(EntityTypeBuilder<EstatusSalida> builder)
    {
        builder.ToTable("EstatusSalida", "inventario");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Seguridad;

namespace Ybridio.Infrastructure.Persistence.Configurations.Seguridad;

internal sealed class ModuloConfiguration : IEntityTypeConfiguration<Modulo>
{
    public void Configure(EntityTypeBuilder<Modulo> builder)
    {
        builder.ToTable("Modulo", "seguridad");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Clave).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Orden).IsRequired();

        builder.Property(e => e.FechaCreacion).IsRequired()
            .HasDefaultValueSql("getdate()");
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

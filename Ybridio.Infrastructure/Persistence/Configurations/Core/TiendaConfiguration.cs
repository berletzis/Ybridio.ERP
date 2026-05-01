using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Core;

namespace Ybridio.Infrastructure.Persistence.Configurations.Core;

internal sealed class TiendaConfiguration : IEntityTypeConfiguration<Tienda>
{
    public void Configure(EntityTypeBuilder<Tienda> builder)
    {
        builder.ToTable("Tienda", "core");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(150);

        builder.Property(e => e.FechaCreacion).IsRequired()
            .HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa)
            .WithMany(e => e.Tiendas)
            .HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_Tienda_Empresa");
    }
}

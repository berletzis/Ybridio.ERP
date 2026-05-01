using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Seguridad;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Infrastructure.Persistence.Configurations.Seguridad;

internal sealed class UsuarioTiendaConfiguration : IEntityTypeConfiguration<UsuarioTienda>
{
    public void Configure(EntityTypeBuilder<UsuarioTienda> builder)
    {
        builder.ToTable("UsuarioTienda", "seguridad");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.HasOne<ApplicationUser>()
            .WithMany(u => u.UsuarioTiendas)
            .HasForeignKey(e => e.UsuarioId)
            .HasConstraintName("FK_UsuarioTienda_Usuario");

        builder.HasOne(e => e.Tienda)
            .WithMany()
            .HasForeignKey(e => e.TiendaId)
            .IsRequired(false);
    }
}

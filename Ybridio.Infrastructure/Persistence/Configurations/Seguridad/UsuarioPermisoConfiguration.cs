using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Seguridad;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Infrastructure.Persistence.Configurations.Seguridad;

internal sealed class UsuarioPermisoConfiguration : IEntityTypeConfiguration<UsuarioPermiso>
{
    public void Configure(EntityTypeBuilder<UsuarioPermiso> builder)
    {
        builder.ToTable("UsuarioPermiso", "seguridad");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        // Permitido nullable: null = hereda rol, true/false = sobrescribe
        builder.Property(e => e.Permitido).IsRequired(false);

        builder.HasOne<ApplicationUser>()
            .WithMany(u => u.UsuarioPermisos)
            .HasForeignKey(e => e.UsuarioId)
            .HasConstraintName("FK_UsuarioPermiso_Usuario");

        builder.HasOne(e => e.Permiso)
            .WithMany(e => e.UsuarioPermisos)
            .HasForeignKey(e => e.PermisoId);
    }
}

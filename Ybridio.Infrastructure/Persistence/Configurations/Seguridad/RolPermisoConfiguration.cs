using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Seguridad;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Infrastructure.Persistence.Configurations.Seguridad;

internal sealed class RolPermisoConfiguration : IEntityTypeConfiguration<RolPermiso>
{
    public void Configure(EntityTypeBuilder<RolPermiso> builder)
    {
        builder.ToTable("RolPermiso", "seguridad");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Permitido).IsRequired().HasDefaultValue(true);

        builder.HasOne<ApplicationRole>()
            .WithMany(r => r.RolPermisos)
            .HasForeignKey(e => e.RolId)
            .HasConstraintName("FK_RolPermiso_Rol");

        builder.HasOne(e => e.Permiso)
            .WithMany(e => e.RolPermisos)
            .HasForeignKey(e => e.PermisoId);
    }
}

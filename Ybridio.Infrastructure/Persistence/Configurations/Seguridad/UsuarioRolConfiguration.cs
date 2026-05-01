using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ybridio.Infrastructure.Persistence.Configurations.Seguridad;

internal sealed class UsuarioRolConfiguration : IEntityTypeConfiguration<IdentityUserRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder)
    {
        builder.ToTable("UsuarioRol", "seguridad");

        builder.HasKey(e => new { e.UserId, e.RoleId });

        builder.Property(e => e.UserId).HasColumnName("UsuarioId");
        builder.Property(e => e.RoleId).HasColumnName("RolId");
    }
}

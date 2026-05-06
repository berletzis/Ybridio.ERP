using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Seguridad;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Infrastructure.Persistence.Configurations.Seguridad;

internal sealed class UsuarioSucursalConfiguration : IEntityTypeConfiguration<UsuarioSucursal>
{
    public void Configure(EntityTypeBuilder<UsuarioSucursal> builder)
    {
        builder.ToTable("UsuarioSucursal", "seguridad");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.HasOne<ApplicationUser>()
            .WithMany(u => u.UsuarioSucursales)
            .HasForeignKey(e => e.UsuarioId)
            .HasConstraintName("FK_UsuarioSucursal_Usuario");

        builder.HasOne(e => e.Sucursal)
            .WithMany()
            .HasForeignKey(e => e.SucursalId)
            .IsRequired(false);
    }
}

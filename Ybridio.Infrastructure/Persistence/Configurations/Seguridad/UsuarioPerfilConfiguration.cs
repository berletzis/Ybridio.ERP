using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Seguridad;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Infrastructure.Persistence.Configurations.Seguridad;

/// <summary>
/// Configuración EF Core para <see cref="UsuarioPerfil"/>.
/// Tabla: seguridad.UsuarioPerfil. Sin soft-delete — eliminación directa.
/// </summary>
internal sealed class UsuarioPerfilConfiguration : IEntityTypeConfiguration<UsuarioPerfil>
{
    public void Configure(EntityTypeBuilder<UsuarioPerfil> builder)
    {
        builder.ToTable("UsuarioPerfil", "seguridad");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.HasOne<ApplicationUser>()
            .WithMany(u => u.UsuarioPerfiles)
            .HasForeignKey(e => e.UsuarioId)
            .HasConstraintName("FK_UsuarioPerfil_Usuario");

        builder.HasOne(e => e.Perfil)
            .WithMany(p => p.UsuarioPerfiles)
            .HasForeignKey(e => e.PerfilId)
            .HasConstraintName("FK_UsuarioPerfil_Perfil");

        builder.HasIndex(e => new { e.UsuarioId, e.PerfilId })
            .IsUnique()
            .HasDatabaseName("UX_UsuarioPerfil_UsuarioPerfil");
    }
}

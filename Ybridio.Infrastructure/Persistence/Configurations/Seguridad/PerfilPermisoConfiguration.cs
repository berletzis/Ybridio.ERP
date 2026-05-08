using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Seguridad;

namespace Ybridio.Infrastructure.Persistence.Configurations.Seguridad;

/// <summary>
/// Configuración EF Core para <see cref="PerfilPermiso"/>.
/// Tabla: seguridad.PerfilPermiso. Sin soft-delete — eliminación directa.
/// </summary>
internal sealed class PerfilPermisoConfiguration : IEntityTypeConfiguration<PerfilPermiso>
{
    public void Configure(EntityTypeBuilder<PerfilPermiso> builder)
    {
        builder.ToTable("PerfilPermiso", "seguridad");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.HasOne(e => e.Perfil)
            .WithMany(p => p.PerfilPermisos)
            .HasForeignKey(e => e.PerfilId)
            .HasConstraintName("FK_PerfilPermiso_Perfil");

        builder.HasOne(e => e.Permiso)
            .WithMany()
            .HasForeignKey(e => e.PermisoId)
            .HasConstraintName("FK_PerfilPermiso_Permiso");

        // Evitar permisos duplicados en un mismo perfil
        builder.HasIndex(e => new { e.PerfilId, e.PermisoId })
            .IsUnique()
            .HasDatabaseName("UX_PerfilPermiso_PerfilPermiso");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Seguridad;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Infrastructure.Persistence.Configurations.Seguridad;

/// <summary>
/// Configuración EF Core para <see cref="Perfil"/>.
/// Tabla: seguridad.Perfil. Perfil hereda de CreationAuditEntity → soft-delete aplicado globalmente.
/// </summary>
internal sealed class PerfilConfiguration : IEntityTypeConfiguration<Perfil>
{
    public void Configure(EntityTypeBuilder<Perfil> builder)
    {
        builder.ToTable("Perfil", "seguridad");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Descripcion).HasMaxLength(500);
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasIndex(e => e.Nombre)
            .IsUnique()
            .HasDatabaseName("UX_Perfil_Nombre");
    }
}

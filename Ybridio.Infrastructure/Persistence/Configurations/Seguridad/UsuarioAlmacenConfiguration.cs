using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Seguridad;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Infrastructure.Persistence.Configurations.Seguridad;

/// <summary>
/// Configuración EF Core para <see cref="UsuarioAlmacen"/>.
/// Tabla: seguridad.UsuarioAlmacen. Sin soft-delete — eliminación directa.
/// </summary>
internal sealed class UsuarioAlmacenConfiguration : IEntityTypeConfiguration<UsuarioAlmacen>
{
    public void Configure(EntityTypeBuilder<UsuarioAlmacen> builder)
    {
        builder.ToTable("UsuarioAlmacen", "seguridad");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.HasOne<ApplicationUser>()
            .WithMany(u => u.UsuarioAlmacenes)
            .HasForeignKey(e => e.UsuarioId)
            .HasConstraintName("FK_UsuarioAlmacen_Usuario");

        builder.HasOne(e => e.Almacen)
            .WithMany()
            .HasForeignKey(e => e.AlmacenId)
            .HasConstraintName("FK_UsuarioAlmacen_Almacen");

        builder.HasIndex(e => new { e.UsuarioId, e.AlmacenId })
            .IsUnique()
            .HasDatabaseName("UX_UsuarioAlmacen_UsuarioAlmacen");

        builder.HasIndex(e => e.UsuarioId)
            .HasDatabaseName("IX_UsuarioAlmacen_UsuarioId");
    }
}

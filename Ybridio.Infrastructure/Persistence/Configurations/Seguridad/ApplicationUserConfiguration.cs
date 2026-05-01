using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Infrastructure.Persistence.Identity;

namespace Ybridio.Infrastructure.Persistence.Configurations.Seguridad;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Usuario", "seguridad");

        builder.Property(e => e.EmpresaId).IsRequired();
        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);

        builder.Property(e => e.FechaCreacion).IsRequired()
            .HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        // Identity columns — sobrescribir longitudes para coincidir con el script
        builder.Property(e => e.Email).HasMaxLength(256);
        builder.Property(e => e.NormalizedEmail).HasMaxLength(256);
        builder.Property(e => e.UserName).HasMaxLength(256);
        builder.Property(e => e.NormalizedUserName).HasMaxLength(256);
        builder.Property(e => e.PasswordHash).HasMaxLength(500);
        builder.Property(e => e.SecurityStamp).HasMaxLength(100);
        builder.Property(e => e.ConcurrencyStamp).HasMaxLength(100);

        builder.Property(e => e.PhoneNumberConfirmed).HasDefaultValue(false);
        builder.Property(e => e.TwoFactorEnabled).HasDefaultValue(false);
        builder.Property(e => e.LockoutEnabled).HasDefaultValue(true);
        builder.Property(e => e.AccessFailedCount).HasDefaultValue(0);

        builder.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_Usuario_Empresa");

        builder.HasIndex(e => e.EmpresaId)
            .HasDatabaseName("IX_Usuario_EmpresaId");
    }
}

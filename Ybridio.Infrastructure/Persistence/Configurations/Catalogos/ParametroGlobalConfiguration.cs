using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Catalogos;

namespace Ybridio.Infrastructure.Persistence.Configurations.Catalogos;

internal sealed class ParametroGlobalConfiguration : IEntityTypeConfiguration<ParametroGlobal>
{
    public void Configure(EntityTypeBuilder<ParametroGlobal> builder)
    {
        builder.ToTable("ParametroGlobal", "catalogos");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.Clave).IsRequired().HasMaxLength(150);
        builder.Property(e => e.Valor).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Descripcion).HasMaxLength(500);
        builder.Property(e => e.TipoDato).IsRequired().HasMaxLength(20).HasDefaultValue("string");
        builder.Property(e => e.Grupo).IsRequired().HasMaxLength(100).HasDefaultValue("General");
        builder.Property(e => e.OrdenVisual).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_ParametroGlobal_Empresa");

        builder.HasIndex(e => new { e.EmpresaId, e.Clave })
            .IsUnique()
            .HasDatabaseName("UQ_ParametroGlobal_EmpresaClave");

        builder.HasIndex(e => e.EmpresaId)
            .HasDatabaseName("IX_ParametroGlobal_EmpresaId");
    }
}

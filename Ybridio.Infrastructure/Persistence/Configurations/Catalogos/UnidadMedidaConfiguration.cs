// ── Ybridio.Infrastructure/Persistence/Configurations/Catalogos/UnidadMedidaConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Catalogos;

namespace Ybridio.Infrastructure.Persistence.Configurations.Catalogos;

internal sealed class UnidadMedidaConfiguration : IEntityTypeConfiguration<UnidadMedida>
{
    public void Configure(EntityTypeBuilder<UnidadMedida> builder)
    {
        builder.ToTable("UnidadMedida", "catalogos");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Abreviatura).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_UnidadMedida_Empresa");
        builder.HasIndex(e => e.EmpresaId).HasDatabaseName("IX_UnidadMedida_EmpresaId");
    }
}
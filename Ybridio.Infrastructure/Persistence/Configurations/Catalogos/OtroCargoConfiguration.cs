using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Catalogos;

namespace Ybridio.Infrastructure.Persistence.Configurations.Catalogos;

internal sealed class OtroCargoConfiguration : IEntityTypeConfiguration<OtroCargo>
{
    public void Configure(EntityTypeBuilder<OtroCargo> builder)
    {
        builder.ToTable("OtroCargo", "catalogos");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.Codigo).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(150);
        builder.Property(e => e.TipoCargo).IsRequired().HasMaxLength(50).HasDefaultValue("Otro");
        builder.Property(e => e.AplicaIva).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.OrdenVisual).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_OtroCargo_Empresa");

        builder.HasOne(e => e.TipoImpuesto).WithMany().HasForeignKey(e => e.TipoImpuestoId)
            .IsRequired(false)
            .HasConstraintName("FK_OtroCargo_TipoImpuesto");

        builder.HasIndex(e => new { e.EmpresaId, e.Codigo })
            .IsUnique()
            .HasDatabaseName("UQ_OtroCargo_EmpresaCodigo");

        builder.HasIndex(e => e.EmpresaId)
            .HasDatabaseName("IX_OtroCargo_EmpresaId");
    }
}

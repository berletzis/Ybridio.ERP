using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Inventario;

namespace Ybridio.Infrastructure.Persistence.Configurations.Inventario;

internal sealed class ConceptoEntradaConfiguration : IEntityTypeConfiguration<ConceptoEntrada>
{
    public void Configure(EntityTypeBuilder<ConceptoEntrada> builder)
    {
        builder.ToTable("ConceptoEntrada", "inventario");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(150);
        builder.Property(e => e.Descripcion).HasMaxLength(500);
        builder.Property(e => e.AfectaExistencia).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.RequiereOrdenCompra).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.EsTraspaso).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_ConceptoEntrada_Empresa");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Finanzas;

namespace Ybridio.Infrastructure.Persistence.Configurations.Finanzas;

public class CategoriaFinancieraConfiguration : IEntityTypeConfiguration<CategoriaFinanciera>
{
    public void Configure(EntityTypeBuilder<CategoriaFinanciera> builder)
    {
        builder.ToTable("CategoriaFinanciera", "finanzas");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.TipoAplicable).IsRequired().HasMaxLength(20).HasDefaultValue("Ambos");
        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Descripcion).HasMaxLength(300);
        builder.Property(e => e.Color).HasMaxLength(20);
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.FechaModificacion);
        builder.Property(e => e.UsuarioModificacionId);
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.EmpresaId, e.Nombre }).HasDatabaseName("IX_CategoriaFinanciera_Empresa_Nombre");
    }
}

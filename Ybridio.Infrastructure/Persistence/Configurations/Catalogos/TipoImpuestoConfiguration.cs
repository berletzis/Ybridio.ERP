using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ybridio.Domain.Catalogos;


namespace Ybridio.Infrastructure.Persistence.Configurations.Catalogos;

internal sealed class TipoImpuestoConfiguration : IEntityTypeConfiguration<TipoImpuesto>
{
    public void Configure(EntityTypeBuilder<TipoImpuesto> builder)
    {
        builder.ToTable("TipoImpuesto", "catalogos");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(150);
        builder.Property(e => e.Porcentaje).IsRequired().HasColumnType("decimal(5,2)");
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_TipoImpuesto_Empresa");
        builder.HasIndex(e => e.EmpresaId).HasDatabaseName("IX_TipoImpuesto_EmpresaId");
    }
}
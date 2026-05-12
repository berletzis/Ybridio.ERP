using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Catalogos;

namespace Ybridio.Infrastructure.Persistence.Configurations.Catalogos;

internal sealed class RelacionComercialConfiguration : IEntityTypeConfiguration<RelacionComercial>
{
    public void Configure(EntityTypeBuilder<RelacionComercial> builder)
    {
        builder.ToTable("RelacionComercial", "core");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.TipoRelacion).IsRequired().HasConversion<int>();
        builder.Property(e => e.LimiteCredito).IsRequired().HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.Observaciones).HasMaxLength(500);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Persona)
            .WithMany()
            .HasForeignKey(e => e.PersonaId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.EmpresaComercial)
            .WithMany()
            .HasForeignKey(e => e.EmpresaComercialId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.EmpresaId, e.TipoRelacion })
            .HasDatabaseName("IX_RelacionComercial_EmpresaId_Tipo");

        builder.HasIndex(e => e.PersonaId)
            .HasDatabaseName("IX_RelacionComercial_PersonaId");

        builder.HasIndex(e => e.EmpresaComercialId)
            .HasDatabaseName("IX_RelacionComercial_EmpresaComercialId");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Catalogos;

namespace Ybridio.Infrastructure.Persistence.Configurations.Catalogos;

internal sealed class EmpresaComercialConfiguration : IEntityTypeConfiguration<EmpresaComercial>
{
    public void Configure(EntityTypeBuilder<EmpresaComercial> builder)
    {
        builder.ToTable("EmpresaComercial", "core");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.RazonSocial).IsRequired().HasMaxLength(300);
        builder.Property(e => e.NombreComercial).HasMaxLength(300);
        builder.Property(e => e.RFC).HasMaxLength(20);
        builder.Property(e => e.Email).HasMaxLength(200);
        builder.Property(e => e.Telefono).HasMaxLength(30);
        builder.Property(e => e.Direccion).HasMaxLength(300);
        builder.Property(e => e.Notas).HasMaxLength(500);
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.EmpresaId, e.RFC })
            .HasDatabaseName("IX_EmpresaComercial_EmpresaId_RFC");

        builder.HasIndex(e => e.EmpresaId)
            .HasDatabaseName("IX_EmpresaComercial_EmpresaId");
    }
}

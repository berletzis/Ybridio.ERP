using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Catalogos;

namespace Ybridio.Infrastructure.Persistence.Configurations.Catalogos;

internal sealed class PersonaConfiguration : IEntityTypeConfiguration<Persona>
{
    public void Configure(EntityTypeBuilder<Persona> builder)
    {
        builder.ToTable("Persona", "core");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Apellidos).HasMaxLength(200);
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

        // NombreCompleto es computed en C#, no persiste
        builder.Ignore(e => e.NombreCompleto);

        builder.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.EmpresaComercial)
            .WithMany(ec => ec.Contactos)
            .HasForeignKey(e => e.EmpresaComercialId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => new { e.EmpresaId, e.RFC })
            .HasDatabaseName("IX_Persona_EmpresaId_RFC");

        builder.HasIndex(e => e.EmpresaId)
            .HasDatabaseName("IX_Persona_EmpresaId");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Catalogos;

namespace Ybridio.Infrastructure.Persistence.Configurations.Catalogos;

internal sealed class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Cliente", "core");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(300);
        builder.Property(e => e.RFC).HasMaxLength(20);
        builder.Property(e => e.Email).HasMaxLength(200);
        builder.Property(e => e.Telefono).HasMaxLength(30);
        builder.Property(e => e.Direccion).HasMaxLength(300);
        builder.Property(e => e.Notas).HasMaxLength(500);
        builder.Property(e => e.LimiteCredito).IsRequired().HasColumnType("decimal(18,2)").HasDefaultValue(0m);

        builder.Property(e => e.FechaCreacion).IsRequired()
            .HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId);

        builder.HasIndex(e => new { e.EmpresaId, e.RFC })
            .HasDatabaseName("IX_Cliente_EmpresaId_RFC");
    }
}

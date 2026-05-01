using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Inventario;

namespace Ybridio.Infrastructure.Persistence.Configurations.Inventario;

internal sealed class AlmacenConfiguration : IEntityTypeConfiguration<Almacen>
{
    public void Configure(EntityTypeBuilder<Almacen> builder)
    {
        builder.ToTable("Almacen", "inventario");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(150);

        builder.Property(e => e.FechaCreacion).IsRequired()
            .HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_Almacen_Empresa");

        builder.HasOne(e => e.Tienda)
            .WithMany()
            .HasForeignKey(e => e.TiendaId)
            .HasConstraintName("FK_Almacen_Tienda");
    }
}

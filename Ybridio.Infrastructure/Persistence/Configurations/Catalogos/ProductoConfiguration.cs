using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Catalogos;

namespace Ybridio.Infrastructure.Persistence.Configurations.Catalogos;

internal sealed class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.ToTable("Producto", "catalogos");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Codigo).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(300);
        builder.Property(e => e.Precio).IsRequired().HasColumnType("decimal(18,6)");
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);

        builder.Property(e => e.FechaCreacion).IsRequired()
            .HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_Producto_Empresa");

        builder.HasIndex(e => new { e.EmpresaId, e.Codigo })
            .IsUnique()
            .HasDatabaseName("UX_Producto_EmpresaId_Codigo");
    }
}

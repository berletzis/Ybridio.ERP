using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Inventario;

namespace Ybridio.Infrastructure.Persistence.Configurations.Inventario;

internal sealed class ExistenciaConfiguration : IEntityTypeConfiguration<Existencia>
{
    public void Configure(EntityTypeBuilder<Existencia> builder)
    {
        builder.ToTable("Existencia", "inventario");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Cantidad).IsRequired().HasColumnType("decimal(18,6)");

        builder.Property(e => e.FechaCreacion).IsRequired()
            .HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_Existencia_Empresa");

        builder.HasOne(e => e.Almacen)
            .WithMany(e => e.Existencias)
            .HasForeignKey(e => e.AlmacenId)
            .HasConstraintName("FK_Existencia_Almacen");

        builder.HasOne(e => e.Producto)
            .WithMany()
            .HasForeignKey(e => e.ProductoId)
            .HasConstraintName("FK_Existencia_Producto");

        // Un producto solo puede tener un registro de existencia por almacén y empresa
        builder.HasIndex(e => new { e.EmpresaId, e.AlmacenId, e.ProductoId })
            .IsUnique()
            .HasDatabaseName("UX_Existencia_EmpresaId_AlmacenId_ProductoId");
    }
}

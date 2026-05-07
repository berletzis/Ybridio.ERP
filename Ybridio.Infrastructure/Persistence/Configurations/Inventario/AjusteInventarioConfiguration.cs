using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Inventario;

namespace Ybridio.Infrastructure.Persistence.Configurations.Inventario;

internal sealed class AjusteInventarioConfiguration : IEntityTypeConfiguration<AjusteInventario>
{
    public void Configure(EntityTypeBuilder<AjusteInventario> builder)
    {
        builder.ToTable("AjusteInventario", "inventario");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Folio).HasMaxLength(50);
        builder.Property(e => e.Fecha).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.TipoAjuste).IsRequired().HasDefaultValue((short)1);
        builder.Property(e => e.Motivo).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Aplicado).IsRequired().HasDefaultValue(false);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId).HasConstraintName("FK_AjusteInventario_Empresa");
        builder.HasOne(e => e.Sucursal).WithMany().HasForeignKey(e => e.SucursalId).HasConstraintName("FK_AjusteInventario_Sucursal");
        builder.HasOne(e => e.Almacen).WithMany().HasForeignKey(e => e.AlmacenId).HasConstraintName("FK_AjusteInventario_Almacen");

        builder.HasMany(e => e.Detalles).WithOne(d => d.AjusteInventario).HasForeignKey(d => d.AjusteInventarioId);
    }
}
